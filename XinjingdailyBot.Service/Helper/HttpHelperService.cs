﻿using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using XinjingdailyBot.Infrastructure.Attribute;
using XinjingdailyBot.Infrastructure.Model;
using XinjingdailyBot.Interface.Helper;

namespace XinjingdailyBot.Service.Helper
{
    [AppService(typeof(IHttpHelperService), LifeTime.Transient)]
    public sealed class HttpHelperService : IHttpHelperService
    {
        private readonly ILogger<HttpHelperService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public HttpHelperService(
            ILogger<HttpHelperService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// 发送网络请求
        /// </summary>
        /// <param name="clientName"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        private async Task<Stream?> SendRequestToStream(string clientName, HttpRequestMessage request)
        {
            try
            {
                var client = _httpClientFactory.CreateClient(clientName);
                var httpRequestMessage = await client.SendAsync(request);
                httpRequestMessage.EnsureSuccessStatusCode();
                var contentStream = await httpRequestMessage.Content.ReadAsStreamAsync();
                return contentStream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "网络请求失败");
                return null;
            }
        }

        /// <summary>
        /// 对象反序列化
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream"></param>
        /// <returns></returns>
        private async Task<T?> StreamToObject<T>(Stream stream)
        {
            try
            {
                T? obj = await JsonSerializer.DeserializeAsync<T>(stream);
                return obj;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "反序列化失败");
                return default;
            }
        }

        public async Task<GitHubReleaseResponse?> GetLatestRelease()
        {
            HttpRequestMessage request = new(HttpMethod.Get, "/XinjingdailyBot/releases/latest");
            using var rawResponse = await SendRequestToStream("GitHub", request);
            if (rawResponse == null)
            {
                return null;
            }
            var response = await StreamToObject<GitHubReleaseResponse>(rawResponse);
            return response;
        }

        public async Task<Stream?> DownloadRelease(string? downloadUrl)
        {
            HttpRequestMessage request = new(HttpMethod.Get, downloadUrl);
            using var rawResponse = await SendRequestToStream("GitHub", request);
            return rawResponse;
        }

        public async Task<IpInfoResponse?> GetIpInformation(IPAddress ip)
        {
            HttpRequestMessage request = new(HttpMethod.Get, $"/{ip}");
            using var rawResponse = await SendRequestToStream("IpInfo", request);
            if (rawResponse == null)
            {
                return null;
            }
            var response = await StreamToObject<IpInfoResponse>(rawResponse);
            return response;
        }
    }
}
