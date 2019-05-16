# The Wiki Game

## About this challenge
This challenge is based around beating the [Wiki Game](https://www.thewikigame.com/). The premise is, given two wikipedia articles, go from one article to the other only clicking the interal links available on the articles. 
For example: Given: Origin = Apple, Target = Earth, Path = Apple > Fruit > Seed > Earth.

### Infrastructure:
* [DynamoDB](https://aws.amazon.com/dynamodb/) will act as our data store.
* [Lambda](https://aws.amazon.com/lambda/) will hold all the business logic.
* Recursive Trigger:
    * Adding a new item to dynamo triggers lambda.
    * Lambda processes the event and adds more items to dynamo.
    * process repeats until exit condition.

#### DB Schema
```
{
  "WikiId": "https://en.wikipedia.org/wiki/Apple::https://en.wikipedia.org/wiki/Seed",
  "CrawlBackLink": "https://en.wikipedia.org/wiki/Fruit",
  "CrawlDepth": 3,
  "CrawlOrigin": "https://en.wikipedia.org/wiki/Apple",
  "CrawlTarget": "https://en.wikipedia.org/wiki/Earth",
  "CrawlUrl": "https://en.wikipedia.org/wiki/Seed"
}
```
* **WikiId:** A unique ID with format: {origin_url}::{current_url}
* **CrawlBackLink:** The article that queued this item
* **CrawlDepth:** Iteration counter
* **CrawlOrigin:** The starting article
* **CrawlTarget:** The destination article
* **CrawlUrl:** The current article

## Setup - .NET, AWS, λ#, Deploy

* [Install .Net 2.1](https://dotnet.microsoft.com/download/dotnet-core/2.1)
* [Sign up for AWS account](https://aws.amazon.com/)
* [Install AWS CLI](https://aws.amazon.com/cli/)
* [Install the λ# Tool](https://github.com/LambdaSharp/LambdaSharpTool#install-%CE%BB-cli)
    * [Documentation](https://lambdasharp.net/articles/ReleaseNotes-Favorinus.html)
    * (Already installed) Upgrade tool
        ```
        dotnet tool update -g LambdaSharp.Tool
        ```

#### Clone
```
git clone git@github.com:LambdaSharp/TheWikiGame.git
```
#### Deploy
```
cd TheWikiGame
lash init --tier wiki       //one time
lash deploy --tier wiki     //to propagate code changes
```


## Preliminary Level  - Trigger Lambda Function and Crawl.
Here are two sample items you can add to the dynamo table to trigger the lambda function. When a solution is found, a final item will be added to the table with a Depth of 0. If a solution is not found, the table will be filled until the Depth reaches 1. 

In order to trigger the lambda function, add the following items into DynamoDB. 

### Sample Trigger Item:
#### (Solution found):
```
{
  "WikiId": "https://fakeurls.com::https://fakeurls.com",
  "CrawlBackLink": "https://fakeurls.com",
  "CrawlDepth": 5,
  "CrawlOrigin": "https://en.wikipedia.org/wiki/Banana",
  "CrawlTarget": "https://en.wikipedia.org/wiki/Lithuania",
  "CrawlUrl": "https://en.wikipedia.org/wiki/Banana"
}
```

#### (No solution found):
```
{
  "WikiId": "https://fakeurls.com::https://fakeurls.com",
  "CrawlBackLink": "https://fakeurls.com",
  "CrawlDepth": 5,
  "CrawlOrigin": "https://en.wikipedia.org/wiki/Banana",
  "CrawlTarget": "https://en.wikipedia.org/wiki/Paint",
  "CrawlUrl": "https://en.wikipedia.org/wiki/Banana"
}
```

> **NOTE**: When adding an item to the DynamoDB table, there is a dropdown on the top left corner to **paste JSON**. 

## 1st Level - Don't add duplicate links!
When picking a link to queue into the table, make sure it doesn't already exist in the table. 
> **NOTE**: The sign that you are adding duplicates is when one of your dynamo items gets overwritten because the key already existed in the table. 

#### Sample (No solution found and demonstrates the duplicate key bug)
```
{
  "WikiId": "https://en.wikipedia.org/wiki/Apple::https://en.wikipedia.org/wiki/Seed",
  "CrawlBackLink": "https://en.wikipedia.org/wiki/Fruit",
  "CrawlDepth": 3,
  "CrawlOrigin": "https://en.wikipedia.org/wiki/Apple",
  "CrawlTarget": "https://en.wikipedia.org/wiki/Earth",
  "CrawlUrl": "https://en.wikipedia.org/wiki/Seed"
}
```

## 2nd Level - Expand, Expand, Expand!
Instead of queueing only one link to the dynamo table, add two. **Be careful, as this can quickly grow exponentially**. Be sure to verify that your Depth parameter is working properly to terminate the recursive calls before trying to add more than 2 links to the table.
> **NOTE**: If your lambda function spirals out of control, you can delete the function. The lambda function can be quickly redeployed using lash. 


## 3rd Level - Add Links Intelligently! (sort of..)
Instead of grabbing the first few links and queueing them into the dynamo table, queue the links that have the most links. For example, if analyzing the article, Apple, grab the link on [Fruits](https://en.wikipedia.org/wiki/Fruit) rather than on [Oral Allergy Syndromes](https://en.wikipedia.org/wiki/Oral_allergy_syndrome), as the page on Fruits has much more links to potentially match the Destination article. 

> **NOTE**: The lambda function is defaulted to a **30 second timeout**. You can modify this in Module.yml and redeploy. If your lambda function is taking a long time to iterate through all the links, consider being more aggressive with the FilterLink function in HelperFunctions.  

## Boss Level - Beat the Game
Improve your app to beat the game! When a solution is found, output the solution in a way where you can complete the game from the [website](https://www.thewikigame.com/). 

