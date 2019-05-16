using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using LambdaSharp;
using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace My.Wiki.Wiki {

    public class Function : ALambdaFunction<DynamoDBEvent, string> {

        private const int LINKS_PER_PAGE = 10;
        private string TABLE_NAME;

        //--- Types ---
        public struct UrlInfo {

            //--- Fields ---
            public Uri Url;
            public Uri BackLink;
            public Uri Origin;
            public Uri Target;
            public int Depth;
            public int Links;
        }

        //--- Fields ---
        private IAmazonDynamoDB _client;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {
            _client = new AmazonDynamoDBClient();
            TABLE_NAME = AwsConverters.ReadDynamoDBTableName(config, "Table");
        }

        public override async Task<string> ProcessMessageAsync(DynamoDBEvent evt) {
            LogInfo(JsonConvert.SerializeObject(evt));
            
            // deserialize INSERT event into UrlInfo object
            foreach (var record in evt.Records.Where(r => "INSERT".Equals(r.EventName))) {
                var urlInfo = new UrlInfo();
                urlInfo.Url = new Uri(record.Dynamodb.NewImage["CrawlUrl"].S);
                urlInfo.BackLink = new Uri(record.Dynamodb.NewImage["CrawlBackLink"].S);
                urlInfo.Origin = new Uri(record.Dynamodb.NewImage["CrawlOrigin"].S);
                urlInfo.Target = new Uri(record.Dynamodb.NewImage["CrawlTarget"].S);
                urlInfo.Depth = Int32.Parse(record.Dynamodb.NewImage["CrawlDepth"].N);

                // call main business logic
                ProcessUrl(urlInfo).Wait();
            }
            return "success";
        }

        public async Task ProcessUrl(UrlInfo urlInfo) {

            // stop processing because we have reached the maximum depth
            if(urlInfo.Depth <= 0) {
                LogInfo($"Ignoring URL '{urlInfo.Url}' because we have reached the maximum depth");
                return;
            }

            // get page contents of wikipedia article
            var httpClient = new HttpClient();
            var url = urlInfo.Url;
            var response = await httpClient.GetAsync(url);
            var strContents = await response.Content.ReadAsStringAsync();

            // iterate through links found in page contents
            var foundLinks = new List<Uri>();
            var matchedLink = false;
            string backLink = null;
            foreach (var link in HelperFunctions.FindLinks(strContents)) {

                // format internal links and attempt to parse for validity
                var llink = HelperFunctions.FixInternalLink(link, urlInfo.Url);
                Uri parsedLink = null;
                try {
                    parsedLink = new Uri(llink);
                } catch {
                    continue;
                }
                
                // ignore external urls
                if (!parsedLink.Host.Equals(urlInfo.Url.Host)) {
                    continue;
                }

                // check for Target article
                if(parsedLink.AbsolutePath == urlInfo.Target.AbsolutePath) {
                    matchedLink = true;
                    backLink = parsedLink.ToString();
                    break;
                }

                // filter out unecessary links
                if(HelperFunctions.FilterLink(parsedLink)) {
                    continue;
                }
                foundLinks.Add(parsedLink);
            }

            var item = new Dictionary<string, AttributeValue>();

            // create solution item
            if(matchedLink) {
                item["WikiId"] = new AttributeValue { S = $"{urlInfo.Origin}::{backLink}"};
                item["CrawlUrl"] = new AttributeValue { S = $"{backLink}"};
                item["CrawlDepth"] = new AttributeValue { N = "0" };
                item["CrawlOrigin"] = new AttributeValue { S = $"{urlInfo.Origin}" };
                item["CrawlTarget"] = new AttributeValue { S = $"{urlInfo.Target}" };
                item["CrawlBackLink"] = new AttributeValue { S = $"{urlInfo.Url}"};
            }

            // only enqueue child links if depth is greater than 1
            if(!matchedLink && urlInfo.Depth > 1) {

                // take the first link and add it to the table
                var linkToQueue = foundLinks.Take(LINKS_PER_PAGE).First();
                item["WikiId"] = new AttributeValue { S = $"{urlInfo.Origin}::{linkToQueue}"};
                item["CrawlUrl"] = new AttributeValue { S = $"{linkToQueue}"};
                item["CrawlDepth"] = new AttributeValue { N = $"{urlInfo.Depth - 1}" };
                item["CrawlOrigin"] = new AttributeValue { S = $"{urlInfo.Origin}" };
                item["CrawlTarget"] = new AttributeValue { S = $"{urlInfo.Target}" };
                item["CrawlBackLink"] = new AttributeValue { S = $"{urlInfo.Url}"};
            }

            // write the item to the table
            if(matchedLink || urlInfo.Depth > 1) {
                try {
                    var writeRequest = new PutItemRequest {
                        TableName = TABLE_NAME,
                        Item = item,
                        ConditionExpression = "WikiId <> :wikiid",
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                            {":wikiid", item["WikiId"]}
                        }
                    };
                    var writeResponse = await _client.PutItemAsync(writeRequest);
                    LogInfo($"writeResponse {JsonConvert.SerializeObject(writeResponse)}");
                }
                catch (Exception e) {
                    LogInfo($"item {JsonConvert.SerializeObject(item)}");
                    LogInfo($"Exception {e}");
                }
            }
        }
    }
}
//new Dictionary<string, List<WriteRequest>>{
//[TABLE_NAME] =  new List<WriteRequest>{
//    new WriteRequest{
//        PutRequest = new PutRequest {
//            Item = item
//        }
//    }
//}
//}