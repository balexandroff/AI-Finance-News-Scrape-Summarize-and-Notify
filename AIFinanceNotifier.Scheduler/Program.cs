using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Threading.Tasks;
using HtmlAgilityPack;

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
            var summary = await SummarizeWithOllamaAsync(article);
            summarizedNews.Add(summary);
        }

        // Step 3: Send email with summarized news
        await SendEmailAsync(summarizedNews);
    }

    static async Task<List<string>> ScrapeYahooFinanceNewsAsync(string url)
    {
        var newsArticles = new List<string>();

        using (var httpClient = new HttpClient())
        {
            // Set a user-agent header to avoid being blocked by Yahoo
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

            var html = await httpClient.GetStringAsync(url);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            // Extract news headlines and links
            var newsNodes = htmlDoc.DocumentNode.SelectNodes("//h3/a[contains(@href, '/news/')]");

            if (newsNodes != null)
            {
                foreach (var node in newsNodes)
                {
                    var headline = node.InnerText.Trim();
                    var link = "https://finance.yahoo.com" + node.GetAttributeValue("href", "").Trim();

                    // Add the headline and link to the list
                    newsArticles.Add($"{headline}\n{link}");
                }
            }
        }

        return newsArticles;
    }

    static async Task<string> SummarizeWithOllamaAsync(string text)
    {
        // Call Ollama's local API for summarization
        using (var httpClient = new HttpClient())
        {
            var requestBody = new { text };
            var response = await httpClient.PostAsJsonAsync("http://localhost:11434/summarize", requestBody);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                return result;
            }
            else
            {
                return "Failed to summarize the news.";
            }
        }
    }

    static async Task SendEmailAsync(List<string> summarizedNews)
    {
        var smtpClient = new SmtpClient("smtp.your-email-provider.com")
        {
            Port = 587,
            Credentials = new System.Net.NetworkCredential("your-email@example.com", "your-email-password"),
            EnableSsl = true,
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress("your-email@example.com"),
            Subject = "Daily Financial News Summary",
            Body = string.Join("\n\n", summarizedNews),
            IsBodyHtml = false,
        };

        mailMessage.To.Add("recipient@example.com");

        await smtpClient.SendMailAsync(mailMessage);
    }
}