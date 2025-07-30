using System;
using System.Collections.Generic;
using OpenAI.Chat;
using Azure.Core;
using Azure.AI.OpenAI;
using Azure;

namespace ApiGraphActivator.Services;

public class OpenAIService
{
    private readonly Uri _endpoint = new Uri("https://bodopenai-eus.openai.azure.com/");
    private readonly string _model = "gpt-4o-mini";
    private readonly string _deploymentName = "gpt-4o-mini";
    private readonly string? _apiKey = Environment.GetEnvironmentVariable("OpenAIKey");
    private readonly AzureOpenAIClient? _azureClient;
    private readonly bool _isConfigured;

    public OpenAIService()
    {
        _isConfigured = !string.IsNullOrEmpty(_apiKey);
        if (_isConfigured)
        {
            _azureClient = new AzureOpenAIClient(
                _endpoint,
                new AzureKeyCredential(_apiKey!));
        }
    }

    public string GetChatResponse(string userInput)
    {
        if (!_isConfigured)
        {
            return "OpenAI service is not configured. Please set the OpenAIKey environment variable.";
        }

        if (_azureClient == null)
        {
            return "OpenAI client is not available.";
        }

        var chatClient = _azureClient.GetChatClient(_deploymentName);

        ChatCompletionOptions requestOptions = new ChatCompletionOptions()
        {
            Temperature = 0.0f,
            TopP = 0.5f,
        };

        var messages = new List<ChatMessage>()
        {
            new SystemChatMessage("""
              You are a financial legal assistant.  Your job is to help the user with their financial legal questions.
              You are not a lawyer, and you cannot give legal advice.  You can only provide information and resources.
              The documents you will be working with are financial documents that are submitted to the SEC.  This documents 
              are 10-Q, 10-K, 8-K, Def14A and other SEC filings.  You will be working with the text of these documents.
              You will not be working with the images.Your main goal will be to extract all of the important information from the financial documents provided.  
              Be clear and concise in your responses.  You must keep the sentiment of the text.  You must not change the meaning of the text.
              You must not add any additional information to the text.  You must not remove any information from the text.
              If you do not know the answer to a question, say "I don't know" and do not try to make up an answer.
              Provide the answer in a paragraph format.  Do not use bullet points or lists.
              Do not use any special characters or formatting.  Do not use any HTML tags or markdown.
              Do not use any code blocks.  
              """
              ),
            new UserChatMessage(userInput)
        };

        ChatCompletion  response = chatClient.CompleteChat(messages, requestOptions);

        return response.Content[0].Text;
    }

    public bool IsConfigured => _isConfigured;
}
