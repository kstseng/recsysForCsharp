using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

/*
 20150413 
 推薦邏輯：在確定推薦某群組下，皆已隨機的方式增加豐富度。
 * 先判斷狀態屬於最小層或商品層，某條件下以第二步決定。
 * 若userID以及狀態皆存在，則暫以3:2的比例推薦商品。
 * 若僅userID存在，則全部以userID的推薦清單為主。
 * 若僅狀態存在，則全部以狀態的推薦清單為主。
 * 若皆不存在，則取前50熱門點擊商品，以亂數的方式挑出10件商品做為推薦項目。
 
 20150511
 * 將使用者目前瀏覽的商品自推薦清單中移除

*/





namespace recsysList
{
    public partial class recommander : System.Web.UI.UserControl
    {
        public string userID { get; set; }
        public string blank { get; set; }
        public string firstProduct { get; set; }
        public string secondProduct { get; set; }

        protected void Page_Load(object sender, EventArgs e)
        {
            //---------------------------

            // Retrieve the storage account from the connection string.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                System.Configuration.ConfigurationManager.AppSettings["StorageConnectionString"]);

            // Create the table client.
            CloudTableClient tableClientState = storageAccount.CreateCloudTableClient();
            CloudTableClient tableClientUser = storageAccount.CreateCloudTableClient();

            // Create the CloudTable object that represents the table.
            CloudTable tableStateMini = tableClientState.GetTableReference("forMinimumLayer");
            CloudTable tableStateModel = tableClientState.GetTableReference("forModel");
            CloudTable tableUser = tableClientUser.GetTableReference("forUser");

            // Create a retrieve operation that takes a entity.
            TableOperation retrieveOperationState = TableOperation.Retrieve<CustomerEntity>(this.firstProduct, this.secondProduct);
            //TableOperation retrieveOperationStateUpdate = new TableOperation();
            TableOperation retrieveOperationUser = TableOperation.Retrieve<CustomerEntity>(this.userID, "blank");

            // Execute the retrieve operation.
            TableResult retrievedResultStateMini = tableStateMini.Execute(retrieveOperationState);
            TableResult retrievedResultStateModel = tableStateModel.Execute(retrieveOperationState);
            TableResult retrievedResultUser = tableUser.Execute(retrieveOperationUser);

            // 預先製作熱銷清單，以防推薦清單中有非產品項目
            // 作法：先將數值向量c(1:40)隨機分派，再取前c個做為欲推薦的商品
            int numOfTop = 40; // choose top c high number of click item
            int numOfCandidate = 15; // choose c candidate
            int[] random = randomNum(numOfTop);
            string[] spareList = new string[numOfCandidate];
            // Create the table client.
            CloudTableClient tableClientHot = storageAccount.CreateCloudTableClient();
            // Create the CloudTable object that represents the table.
            CloudTable tableHot = tableClientHot.GetTableReference("forHotItem");

            for (int r = 0; r < numOfCandidate; r++)
            {
                // Create a retrieve operation that takes a entity.
                TableOperation retrieveOperationHot = TableOperation.Retrieve<CustomerEntity>(random[r].ToString(), "blank");
                // Execute the retrieve operation.
                TableResult retrievedResultHot = tableHot.Execute(retrieveOperationHot);
                spareList[r] = ((CustomerEntity)retrievedResultHot.Result).recList;
            }


            // 判斷是最小層或是商品層
            TableResult retrievedResultState = new TableResult();
            if (retrievedResultStateMini.Result != null)
            {
                retrievedResultState = tableStateMini.Execute(retrieveOperationState);
            }
            else if (retrievedResultStateModel.Result != null)
            {
                retrievedResultState = tableStateModel.Execute(retrieveOperationState);
            }
            else
            {
                // 考慮狀態中若同時存在最小層和商品層，則取最新的狀態(secondProduct)，視為單一狀態下的推薦清單。
                TableOperation retrieveOperationStateUpdate = TableOperation.Retrieve<CustomerEntity>("BLANK", this.secondProduct);
                TableResult retrievedResultStateUpdateMini = tableStateMini.Execute(retrieveOperationStateUpdate);
                TableResult retrievedResultStateUpdateModel = tableStateModel.Execute(retrieveOperationStateUpdate);
                if (retrievedResultStateUpdateMini.Result != null)
                {
                    //secondProduct is minimumLayer.
                    retrievedResultState = tableStateMini.Execute(retrieveOperationStateUpdate);
                }
                else if (retrievedResultStateUpdateModel.Result != null)
                {
                    //secondProduct is Model.
                    retrievedResultState = tableStateModel.Execute(retrieveOperationStateUpdate);
                }
                else
                {
                    //secondProduct is neither munimumLayer nor Model.
                    retrievedResultState = tableStateMini.Execute(retrieveOperationState);
                }
            }

            // 確認userID是否存在，若存在則按比例分配推薦項目清單；否則，全部由狀態決定推薦項目清單
            int numOfRec = 5; int numOfHot = 5; int defaultLen = 5;
            string[] recListFinal1 = new string[numOfRec];
            string[] recListFinal = new string[numOfRec + numOfHot]; // First numOfRec are rec products, last 10 are hot products.
            if (retrievedResultUser.Result != null)
            {
                if (retrievedResultState.Result != null)
                {
                    // 使用者清單取亂數index = 0, 1, 2的產品，狀態清單取亂數index = 0, 1的產品(暫)
                    // User
                    // + 1的原因是避免當前的產品出現在推薦清單中
                    // byUser 4取3、byState 3取2
                    int userRec = 3; 
                    int numOfRecByUserDefault = userRec + 1;
                    string[] recListUser = ((CustomerEntity)retrievedResultUser.Result).recList.Split(',');
                    int numOfRecByUser = Math.Min(recListUser.Length, numOfRecByUserDefault);
                    string[] recListUserRandom = new string[numOfRecByUser];
                    int[] top1 = randomNum(numOfRecByUser);
                    for (int i = 0; i < numOfRecByUser; i++)
                    {
                        recListUserRandom[i] = recListUser[top1[i]];
                    }
                    recListUserRandom = recListUserRandom.Where(s => s != secondProduct).Take(numOfRecByUser - 1).ToArray();

                    // State
                    int stateRec = 2;
                    int numOfRecByStateDefault = stateRec + 1;
                    string[] recListState = ((CustomerEntity)retrievedResultState.Result).recList.Split(';');
                    int numOfRecByState = Math.Min(recListState.Length, numOfRecByStateDefault);
                    string[] recListStateRandom = new string[numOfRecByState];
                    int[] top2 = randomNum(numOfRecByState);
                    for (int i = 0; i < numOfRecByState; i++)
                    {
                        recListStateRandom[i] = recListState[top2[i]];
                    }
                    recListStateRandom = recListStateRandom.Where(s => s != secondProduct).Take(numOfRecByState - 1).ToArray();

                    recListFinal1 = recListUserRandom.Union(recListStateRandom).ToArray();
                    recListFinal = recListFinal1.Union(spareList.Where(s => s != secondProduct)).Where(s => s != null).Take(10).ToArray();
                }
                else
                {
                    int defaultRecLength = defaultLen + 1;
                    string[] recListUser = ((CustomerEntity)retrievedResultUser.Result).recList.Split(',');
                    int numOfRecByUser = Math.Min(recListUser.Length, defaultRecLength);
                    int[] top = randomNum(numOfRecByUser);
                    for (int i = 0; i < numOfRecByUser; i++)
                    {
                        recListFinal[i] = recListUser[top[i]];
                    }
                    recListFinal = recListFinal.Where(s => s != secondProduct).ToArray();
                    recListFinal = recListFinal.Union(spareList.Where(s => s != secondProduct)).Where(s => s != null).Take(10).ToArray();
                }
            }
            else
            {
                // 若userID不存在，再判斷狀態是否存在，是則全部推薦清單由狀態清單產生，否則回傳"nothing"
                // 因為目前從State吐出的推薦項目只有5項，其他都是10項，所以寫法稍有不同。
                // int defaultRecLength = defaultLen;
                if (retrievedResultState.Result != null)
                {
                    string[] recListState = ((CustomerEntity)retrievedResultState.Result).recList.Split(';');
                    int numOfRecByState = Math.Min(recListState.Length, defaultLen);
                    int[] top = randomNum(numOfRecByState);
                    for (int i = 0; i < numOfRecByState; i++)
                    {
                        recListFinal[i] = recListState[top[i]];
                    }

                    recListFinal = recListFinal.Where(s => s != secondProduct).ToArray();
                    recListFinal = recListFinal.Union(spareList.Where(s => s != secondProduct)).Where(s => s != null).Take(10).ToArray();
                }
                else
                {
                    // userID和狀態皆不存在，推薦熱門點擊商品。取前h高的熱門點擊商品進行隨機推薦。
                    recListFinal = spareList;
                    recListFinal = recListFinal.Where(s => s != secondProduct).Take(10).ToArray();
                }
            }

            //---------------------------

            foreach (string recItem in recListFinal.ToList())
            {
                Response.Write("<br />" + recItem);
            }

        }


        public class CustomerEntity : TableEntity
        {
            public CustomerEntity(string pKey, string rKey)
            {
                this.PartitionKey = pKey;
                this.RowKey = rKey;
            }
            public CustomerEntity() { }
            public string recList { get; set; }
        }

        // 給定範圍之不重複隨機亂數
        public int[] randomNum(int len)
        {
            // 利用亂數增加商品豐富度
            //int len = 10;
            Random rnd = new Random();
            int[] randomize = new int[len];
            for (int i = 0; i < len; i++)
            {
                randomize[i] = rnd.Next(0, len);
                for (int j = 0; j < i; j++)
                {
                    while (randomize[j] == randomize[i])
                    {
                        j = 0;
                        randomize[i] = rnd.Next(0, len);
                    }
                }
            }
            int[] top = new int[len];
            for (int k = 0; k < len; k++)
            {
                top[k] = randomize.ToList().IndexOf(k);
            }

            return top;
        }

    }
}