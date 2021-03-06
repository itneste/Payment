﻿using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Essensoft.AspNetCore.Payment.Security;
using Essensoft.AspNetCore.Payment.WeChatPay.Notify;
using Essensoft.AspNetCore.Payment.WeChatPay.Parser;
using Essensoft.AspNetCore.Payment.WeChatPay.Utility;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Essensoft.AspNetCore.Payment.WeChatPay
{
    public class WeChatPayNotifyClient : IWeChatPayNotifyClient
    {
        public virtual ILogger Logger { get; set; }

        public WeChatPayOptions Options { get; }

        #region WeChatPayNotifyClient Constructors

        public WeChatPayNotifyClient(
            ILogger<WeChatPayNotifyClient> logger,
            IOptions<WeChatPayOptions> optionsAccessor)
        {
            Logger = logger;
            Options = optionsAccessor.Value;

            if (string.IsNullOrEmpty(Options.Key))
            {
                throw new ArgumentNullException(nameof(Options.Key));
            }
        }

        #endregion

        #region IWeChatPayNotifyClient Members

        public async Task<T> ExecuteAsync<T>(HttpRequest request) where T : WeChatPayNotifyResponse
        {
            var body = await new StreamReader(request.Body, Encoding.UTF8).ReadToEndAsync();
            Logger?.LogTrace(0, "Request:{body}", body);

            var parser = new WeChatPayXmlParser<T>();
            var rsp = parser.Parse(body);
            if (rsp is WeChatPayRefundNotifyResponse)
            {
                var key = MD5.Compute(Options.Key).ToLower();
                var data = AES.Decrypt((rsp as WeChatPayRefundNotifyResponse).ReqInfo, key, AESCipherMode.ECB, AESPaddingMode.PKCS7);
                Logger?.LogTrace(1, "Decrypt Content:{data}", data); // AES-256-ECB
                rsp = parser.Parse(body, data);
            }
            else
            {
                CheckNotifySign(rsp);
            }
            return rsp;
        }

        #endregion

        #region Common Method

        private void CheckNotifySign(WeChatPayNotifyResponse response)
        {
            if (response?.Parameters?.Count == 0)
            {
                throw new Exception("sign check fail: Body is Empty!");
            }

            if (!response.Parameters.TryGetValue("sign", out var sign))
            {
                throw new Exception("sign check fail: sign is Empty!");
            }

            var cal_sign = WeChatPaySignature.SignWithKey(response.Parameters, Options.Key);
            if (cal_sign != sign)
            {
                throw new Exception("sign check fail: check Sign and Data Fail!");
            }
        }

        #endregion
    }
}