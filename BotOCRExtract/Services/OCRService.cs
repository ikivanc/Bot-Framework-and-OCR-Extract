using BotOCRExtract.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace BotOCRExtract.Services
{
    public class OCRService
    {

        public static async Task<List<string>> MakeAnalysisWithImage(Stream stream,string subscriptionKey,string uriEndPoint)
        {
            List<string> result = new List<string>();
            HttpClient client = new HttpClient();

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

            string requestParameters = String.Format("?mode=Printed");

            // Assemble the URI for the REST API method.
            string uri = uriEndPoint + requestParameters;

            HttpResponseMessage response;

            BinaryReader binaryReader = new BinaryReader(stream);
            byte[] byteData = binaryReader.ReadBytes((int)stream.Length);

            using (ByteArrayContent content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                // Asynchronously call the REST API method.
                response = await client.PostAsync(uri, content);
            }

            string operationLocation = null;

            // The response contains the URI to retrieve the result of the process.
            if (response.IsSuccessStatusCode)
            {
                operationLocation = response.Headers.GetValues("Operation-Location").FirstOrDefault();
            }

            string contentString;
            int i = 0;
            do
            {
                System.Threading.Thread.Sleep(1000);
                response = await client.GetAsync(operationLocation);
                contentString = await response.Content.ReadAsStringAsync();
                ++i;
            }
            while (i < 10 && contentString.IndexOf("\"status\":\"Succeeded\"") == -1);


            string json = response.Content.ReadAsStringAsync().Result;
            json = json.TrimStart(new char[] { '[' }).TrimEnd(new char[] { ']' });
            RecognizeText ocrOutput = JsonConvert.DeserializeObject<RecognizeText>(json);

            if (ocrOutput != null && ocrOutput.RecognitionResult != null && ocrOutput.RecognitionResult.Lines != null)
            {

                List<string> resultText = new List<string>();

                resultText = (from Line sline in ocrOutput.RecognitionResult.Lines
                              select (string)sline.Text).ToList<string>();

                resultText = OCRHelper.ExtractKeyValuePairs(ocrOutput.RecognitionResult.Lines);

                return resultText;
            }
            else
            {
                return null;
            }
        }

    }
}
