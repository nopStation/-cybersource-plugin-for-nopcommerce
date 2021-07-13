using System;
using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Nop.Plugin.Payments.CyberSource
{
    public class HostedPaymentHelper
    {
        #region Properties

        internal static string OrderPageTimestamp
        {
            get
            {
                return (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds.ToString().Split('.')[0];
            }
        }

        #endregion

        #region Methods

        public static string CalcRequestSign(NameValueCollection reqParams, string publicKey)
        {
            var sb = new StringBuilder();

            sb.Append(reqParams["merchantID"]);
            sb.Append(reqParams["amount"]);
            sb.Append(reqParams["currency"]);
            sb.Append(reqParams["orderPage_timestamp"]);
            sb.Append(reqParams["orderPage_transactionType"]);

            return CalcHMACSHA1Hash(sb.ToString(), publicKey).Replace("\n", "");
        }

        public static bool ValidateResponseSign(IFormCollection rspParams, string publicKey)
        {
            string transactionSignature;
            string[] signedFields;

            try
            {
                transactionSignature = rspParams["transactionSignature"];
                signedFields = rspParams["signedFields"].ToString().Split(',');
            }
            catch (Exception)
            {
                return false;
            }

            var sb = new StringBuilder();
            
            foreach (var signedFild in signedFields)
            {
                sb.Append(rspParams[signedFild]);
            }

            return transactionSignature.Equals(CalcHMACSHA1Hash(sb.ToString(), publicKey));
        }

        #endregion

        #region Utilities

        private static string CalcHMACSHA1Hash(string s, string publicKey)
        {
            using (var cs = new HMACSHA1(Encoding.UTF8.GetBytes(publicKey)))
            {
                return Convert.ToBase64String(cs.ComputeHash(Encoding.UTF8.GetBytes(s)));
            }
        }

        #endregion
    }
}
