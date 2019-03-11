using BotOCRExtract.Model;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace BotOCRExtract.Helpers
{
    public class Helper
    {
        public static List<TextExtract> RetrieveAllSearchTextKeyFields()
        {
            //json data path
            var file = File.ReadAllText("Resources/SearchData.json");

            //deserialize JSON from file  
            string Json = file;
            var searchtextlist = JsonConvert.DeserializeObject<List<TextExtract>>(Json);

            return searchtextlist;
        }
    }
}
