using HtmlAgilityPack;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

class Program
{
    static async Task Main(string[] args)
    {
        // Step 1: Scrape financial news from Yahoo Finance
        var yahooFinanceUrl = "https://finance.yahoo.com/topic/stock-market-news/";
        var newsArticles = await ScrapeYahooFinanceNewsAsync(yahooFinanceUrl);

        // Step 2: Summarize news using Ollama
        var summarizedNews = new List<string>();
        foreach (var article in newsArticles)
        {
            var summary = await SummarizeWithOllamaAsync(article.title, article.content, article.url);
            summarizedNews.Add(summary);
        }

        // Step 3: Send email with summarized news
        await SendEmailAsync(summarizedNews);
    }

    static async Task<List<(string title, string content, string url)>> ScrapeYahooFinanceNewsAsync(string url)
    {
        List<(string title, string content, string url)> newsArticles = new List<(string title, string content, string url)>();

        using (var httpClient = new HttpClient())
        {
            // Set a user-agent header to avoid being blocked by Yahoo
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

            var html = await httpClient.GetStringAsync(url);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            var articleLInks = new List<string>();

            // Extract news headlines and links
            var leadNews = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'topic-hero-lead')]");
            var topicNews = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'topic-stories')]");

            //Scrape Lewd News
            var leadNewsLink = leadNews.SelectSingleNode("//a[contains(@class, 'titles-link')]");
            if (leadNewsLink != null)
            {
                var leadNewsUrl = leadNewsLink.GetAttributeValue<string>("href", string.Empty);
                if (!string.IsNullOrEmpty(leadNewsUrl))
                    articleLInks.Add(leadNewsUrl);
            }

            foreach(var topicNewsItem in topicNews.SelectNodes("section[contains(@class, 'container')]"))
            {
                var linkNode = topicNewsItem.SelectSingleNode("a[contains(@class, 'subtle-link')]");
                if (linkNode != null)
                {
                    var linkUrl = linkNode.GetAttributeValue<string>("href", string.Empty);
                    if (!string.IsNullOrEmpty(linkUrl))
                        articleLInks.Add(linkUrl);
                }
            }

            foreach(var link in articleLInks)
            {
                using (var httpArticleClient = new HttpClient())
                {
                    // Set a user-agent header to avoid being blocked by Yahoo
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

                    var articleHtml = await httpArticleClient.GetStringAsync(link);
                    var articleHtmlDoc = new HtmlDocument();
                    articleHtmlDoc.LoadHtml(articleHtml);

                    var articleTitle = articleHtmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'article-wrap')]//div[contains(@class, 'cover-wrap')]//div[contains(@class, 'cover-title')]").InnerText;
                    var articleContent = articleHtmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'article-wrap')]//div[contains(@class, 'body-wrap')]//div[contains(@class, 'body')]").InnerText;

                    newsArticles.Add((articleTitle, articleContent, link));
                }

                Thread.Sleep(5000);
            }
        }

        return newsArticles;
    }

    static async Task<string> SummarizeWithOllamaAsync(string articleTitle, string articleContent, string url)
    {
        StringBuilder responseBuilder = new StringBuilder();

        // Call Ollama's local API for summarization
        using (var httpClient = new HttpClient())
        {
            /****************************************************************************************************************/
            // Summarized by GPT OLlama gemma2 model
            /****************************************************************************************************************/
            try
            {
                responseBuilder.AppendLine($"<b>{articleTitle}</b> <br />");
                responseBuilder.AppendLine();

                var requestBody = new
                {
                    model = "gemma",  // Change this to your preferred model
                    prompt = $"Summarize me the following finance news in no more than 50 words. Highlite in bold the most important words of the summary using html tag '<b></b>' - Title: '{articleTitle}', Content: '{articleContent}'",
                    stream = true
                };
                string json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await httpClient.PostAsync("http://localhost:11434/api/generate", content))
                using (Stream stream = await response.Content.ReadAsStreamAsync())
                using (StreamReader reader = new StreamReader(stream))
                {
                    while (!reader.EndOfStream)
                    {
                        string? line = await reader.ReadLineAsync();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            try
                            {
                                using JsonDocument doc = JsonDocument.Parse(line);
                                string token = doc.RootElement.GetProperty("response").GetString();
                                responseBuilder.Append(token);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"JSON parse error: {ex.Message}");
                            }
                        }
                    }

                    responseBuilder.AppendLine($" For reference - <a href=\"{url}\">Read more</a>");
                    responseBuilder.AppendLine("<br /><br />");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }

            return responseBuilder.ToString();

            /****************************************************************************************************************/
            // Summarie by BART Transformer - facebook/bart-large-cnn
            /****************************************************************************************************************/

            try
            {
                responseBuilder.AppendLine("<b>Summarized by BART Transformer - facebook/bart-large-cnn.</b><br /><br />");
                responseBuilder.AppendLine();

                var requestBody = new
                {
                    text = $"'{articleTitle}' \r\n '{articleContent}'"

                };
                string json = JsonSerializer.Serialize(requestBody);
                var contentTransformer = new StringContent(json, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await httpClient.PostAsync("http://localhost:8000/api/summarize", contentTransformer))
                { 
                    var result = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        try
                        {
                            using JsonDocument doc = JsonDocument.Parse(result);
                            doc.RootElement.TryGetProperty("summary", out JsonElement value);
                            responseBuilder.Append(value.ToString());
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"JSON parse error: {ex.Message}");
                        }
                    }
                }

                responseBuilder.AppendLine($" For reference - <a href=\"{url}\">Read more</a>");
                responseBuilder.AppendLine("<br /><br />");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }

            return responseBuilder.ToString();
        }
    }

    static async Task SendEmailAsync(List<string> summarizedNews)
    {
        try
        {
            var smtpClient = new SmtpClient("mail.codixit.com")
            {
                Port = 25,
                Credentials = new System.Net.NetworkCredential("contact@codixit.com", "BmwMP0w3r"),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress("contact@codixit.com"),
                Subject = "Daily Financial News Summary",
                Body = string.Join("\n\n", summarizedNews),
                IsBodyHtml = true,
            };

            mailMessage.To.Add("borko.alexandrov@gmail.com");

            await smtpClient.SendMailAsync(mailMessage);
        }
        catch(Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}