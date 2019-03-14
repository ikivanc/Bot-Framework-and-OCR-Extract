using BotOCRExtract.Model;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Bot.Connector.Authentication;
using System;
using Microsoft.Bot.Schema;

namespace BotOCRExtract.Services
{
    public class OCRHelper
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

        public static List<string> ExtractKeyValuePairs(Line[] lines)
        {
            //Initialize settings
            List<TextExtract> searchKeyList = OCRHelper.RetrieveAllSearchTextKeyFields(); // Retrieve all key-fields as reference
            List<Word> textvalues = new List<Word>();
            List<string> result = new List<string>();
            int x_margin, y_margin, x_width, y_height;

            // Extract regions of text words
            foreach (Line sline in lines)
            {
                foreach (Word sword in sline.Words)
                {
                    int[] wvalues = sword.BoundingBox;
                    textvalues.Add(new Word { Text = sword.Text, BoundingBox = wvalues });
                }
            }

            // Search Key-Value Pairs inside the documents
            if (searchKeyList.Count > 0)
            {
                foreach (TextExtract key in searchKeyList)
                {
                    var resultkeys = textvalues.Where(a => a.Text.Contains(key.Text));
                    foreach (var fieldtext in resultkeys)
                    {
                        // Assign all fields values per text
                        x_margin = key.MarginX;
                        y_margin = key.MarginY;
                        x_width = key.Width;
                        y_height = key.Height;

                        // For every value candidate set all values above
                        string txtreply = string.Join(" ",
                                                    from a in textvalues
                                                    where
                                                    (a.BoundingBox[0] >= fieldtext.BoundingBox[0] + x_margin) &&
                                                    (a.BoundingBox[0] <= fieldtext.BoundingBox[0] + x_margin + x_width) &&
                                                    (a.BoundingBox[1] >= fieldtext.BoundingBox[1] - y_margin) &&
                                                    (a.BoundingBox[1] <= fieldtext.BoundingBox[1] + y_height)
                                                    select (string)a.Text);
                        result.Add(fieldtext.Text + " - " + txtreply);
                    }
                }
                return result;
            }
            else
            {
                return null;
            }
        }

        //Methods for Image Upload 
        public static async Task<List<string>> GetCaptionAsync(Activity activity, ConnectorClient connector, string subscriptionKey, string uriEndPoint)
        {
            var imageAttachment = activity.Attachments?.FirstOrDefault(a => a.ContentType.Contains("image"));
            if (imageAttachment != null)
            {
                //if serviceUrl doesn't show in filename 
                var localFileName = imageAttachment.ContentUrl.Replace("[object Promise]", activity.ServiceUrl);


                using (var stream = await GetImageStream(connector, localFileName))
                {
                    return await OCRService.MakeAnalysisWithImage(stream, subscriptionKey, uriEndPoint);
                }
            }

            // If we reach here then the activity is neither an image attachment nor an image URL.
            throw new ArgumentException("The activity doesn't contain a valid image attachment or an image URL.");
        }

        private static async Task<Stream> GetImageStream(ConnectorClient connector, string imageUrl)
        {
            using (HttpClient client = new HttpClient())
            {

                var uri = new Uri(imageUrl);
                if (uri.Host.EndsWith("skype.com") && uri.Scheme == "https")
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(connector));
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

                    return await client.GetStreamAsync(uri);
                }
                else
                {
                    var response = await client.GetAsync(imageUrl);
                    if (!response.IsSuccessStatusCode) return null;
                    return await response.Content?.ReadAsStreamAsync();
                }
            }
        }

        private static async Task<string> GetTokenAsync(ConnectorClient connector)
        {
            var credentials = connector.Credentials as MicrosoftAppCredentials;
            if (credentials != null)
            {
                return await credentials.GetTokenAsync();
            }

            return null;
        }


    }
}
