using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace Nop.Plugin.Payments.BluePay
{
    /// <summary>
    /// The BluePay manager.
    /// </summary>
    public class BluePayManager
    {
        private NameValueCollection _requestParams;
        private NameValueCollection _responseParams;

        #region Request properties

        // general info
        public string AccountId { get; set; }
        public string UserId { get; set; }
        public string SecretKey { get; set; }
        public bool IsSandbox { get; set; }

        // details info
        public string InvoiceId { get; set; }
        public string OrderId { get; set; }
        public string AmountTip { get; set; }
        public string AmountTax { get; set; }
        public string AmountFood { get; set; }
        public string AmountMisc { get; set; }
        
        // customer info
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
        public string Zip { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string CustomerIP { get; set; }
        public string CustomId1 { get; set; }
        public string CustomId2 { get; set; }

        // payment info
        public string CardNumber { get; set; }
        public string CardCvv2 { get; set; }
        public string CardExpire { get; set; }

        // transaction info
        public string Amount { get; set; }
        public string TransactionType { get; set; }
        public string PaymentType { get; set; }
        public string MasterId { get; set; }

        // rebill info
        public string DoRebill  { get; set; }
        public string RebillAmount { get; set; }
        public string RebillFirstDate { get; set; }
        public string RebillExpression { get; set; }
        public string RebillCycles { get; set; }

        #endregion

        #region Response properties

        /// <summary>
        /// Returns true when a request is successful
        /// </summary>
        public bool IsSuccessful
        {
            get { return (Status == "1" && Message != "DUPLICATE"); }
        }

        /// <summary>
        /// Returns true when a recurring payment is successful canceled
        /// </summary>
        public bool IsSuccessfulCancelRecurring
        {
            get { return (Status == "stopped" || Status == "deleted"); }
        }

        /// <summary>
        /// Returns a status code from response ("1" for APPROVED, "0" for DECLINE, "E" and all other responses are ERROR.)
        /// </summary>
        public string Status
        {
            get { return _responseParams["STATUS"] ?? string.Empty; }
        }

        /// <summary>
        /// Returns the ID assigned to the transaction
        /// </summary>
        public string TransactionId
        {
            get { return _responseParams["TRANS_ID"] ?? string.Empty; }
        }

        /// <summary>
        /// Returns a human-readable description of the transaction status from response
        /// </summary>
        public string Message
        {
            get { return _responseParams["MESSAGE"] ?? string.Empty; }
        }

        /// <summary>
        /// Returns the Address Verification System (AVS) response code received on the transaction (
        /// A - Partial match - Street Address matches, ZIP Code does not;
        /// B - International street address match, postal code not verified due to incompatible formats;
        /// C - International street address and postal code not verified due to incompatible formats;
        /// D - International street address and postal code match;
        /// E - Not a mail or phone order;
        /// F - Address and Postal Code match (UK only);
        /// G - Service Not supported, non-US Issuer does not participate;
        /// I - Address information not verified for international transaction;
        /// M - Address and Postal Code match;
        /// N - No match - No Address or ZIP Code match;
        /// P - International postal code match, street address not verified due to incompatible format;
        /// Q - Bill to address did not pass edit checks/Card Association can't verify the authentication of an address;
        /// R - Retry - Issuer system unavailable, retry later;
        /// S - Service not supported;
        /// W - Partial match - ZIP Code matches, Street Address does not;
        /// U - Unavailable - Address information is unavailable for that account number, or the card issuer does not support;
        /// X - Exact match, 9 digit zip - Street Address, and 9 digit ZIP Code match;
        /// Y - Exact match, 5 digit zip - Street Address, and 5 digit ZIP Code match;
        /// Z - Partial match - 5 digit ZIP Code match only;
        /// 1 - Cardholder name matches;
        /// 2 - Cardholder name, billing address, and postal code match;
        /// 3 - Cardholder name and billing postal code match;
        /// 4 - Cardholder name and billing address match;
        /// 5 - Cardholder name incorrect, billing address and postal code match;
        /// 6 - Cardholder name incorrect, billing postal code matches;
        /// 7 - Cardholder name incorrect, billing address matches;
        /// 8 - Cardholder name, billing address, and postal code are all incorrect)
        /// </summary>
        public string AVS
        {
            get { return _responseParams["AVS"] ?? string.Empty; }
        }

        /// <summary>
        /// Returns the Card Verification Value 2 response code (
        /// _ = Unsupported on this network or transaction type;
        /// M = CVV2 Match;
        /// N = CVV2 did not match;
        /// P = CVV2 was not processed;
        /// S = CVV2 exists but was not input;
        /// U = Card issuer does not provide CVV2 service;
        /// X = No response from association)
        /// </summary>
        public string CVV2
        {
            get { return _responseParams["CVV2"] ?? string.Empty; }
        }

        /// <summary>
        /// Returns the ID of the newly-created rebilling sequence
        /// </summary>
        public string RebillId
        {
            get { return _responseParams["REBID"] ?? string.Empty; }
        }

        /// <summary>
        /// Returns the auth code for successful AUTH transaction
        /// </summary>
        public string AuthCode
        {
            get { return _responseParams["AUTH_CODE"] ?? string.Empty; }
        }

        /// <summary>
        /// Returns the used credit card, masked with 'X' as appropriate
        /// </summary>
        public string CardMask
        {
            get { return _responseParams["PAYMENT_ACCOUNT_MASK"] ?? string.Empty; }
        }

        /// <summary>
        /// Returns the type of credit card (VISA, MC, DISC, AMEX, ACH, etc)
        /// </summary>
        public string CardType
        {
            get { return _responseParams["CARD_TYPE"] ?? string.Empty; }
        }

        /// <summary>
        /// Returns the country of credit card issuer
        /// </summary>
        public string CardCountry
        {
            get { return _responseParams["CARD_COUNTRY"] ?? string.Empty; }
        }

        /// <summary>
        /// Returns the identification number of bank that issued the card
        /// </summary>
        public string BankId
        {
            get { return _responseParams["BIN"] ?? string.Empty; }
        }

        /// <summary>
        /// Returns the customer's bank name
        /// </summary>
        public string BankName
        {
            get { return _responseParams["BANK_NAME"] ?? string.Empty; }
        }

        /// <summary>
        /// Returns the tilde (~) seperated list of transaction information returned by credit card processing network.
        /// (Example: 6~V~X~~~~~~~~A~N~~~Y~C)
        /// </summary>
        public string BankInformation
        {
            get { return _responseParams["BINDATA"] ?? string.Empty; }
        }

        #endregion

        public BluePayManager()
        {
            _requestParams = HttpUtility.ParseQueryString(string.Empty);
            _responseParams = new NameValueCollection();
        }

        /// <summary>
        /// Authorization or sale (authorization and capture) transaction
        /// </summary>
        /// <param name="capture">Use capture after authorization transaction</param>
        public void Sale(bool capture)
        {
            _requestParams.Add("PAYMENT_TYPE", "CREDIT");
            _requestParams.Add("AMOUNT", Amount);
            _requestParams.Add("PAYMENT_ACCOUNT", CardNumber);
            _requestParams.Add("CARD_EXPIRE", CardExpire);
            _requestParams.Add("CARD_CVV2", CardCvv2);   

            _requestParams.Add("NAME1", FirstName);
            _requestParams.Add("NAME2", LastName);
            _requestParams.Add("ADDR1", Address1);
            _requestParams.Add("ADDR2", Address2);
            _requestParams.Add("CITY", City);
            _requestParams.Add("STATE", State);
            _requestParams.Add("COUNTRY", Country);
            _requestParams.Add("ZIP", Zip);
            _requestParams.Add("EMAIL", Email);
            _requestParams.Add("PHONE", Phone);
            _requestParams.Add("CUSTOM_ID", CustomId1);
            _requestParams.Add("CUSTOM_ID2", CustomId2);
            _requestParams.Add("CUSTOMER_IP", CustomerIP);
            _requestParams.Add("ORDER_ID", OrderId);
            _requestParams.Add("INVOICE_ID", InvoiceId);

            Post20API(capture ? "SALE" : "AUTH");
        }

        /// <summary>
        /// Capture transaction
        /// </summary>
        public void Capture()
        {
            _requestParams.Add("MASTER_ID", MasterId);
            _requestParams.Add("AMOUNT", Amount);

            Post20API("CAPTURE");
        }

        /// <summary>
        /// Refund transaction
        /// </summary>
        public void Refund()
        {
            _requestParams.Add("MASTER_ID", MasterId);
            _requestParams.Add("AMOUNT", Amount);

            Post20API("REFUND");
        }

        /// <summary>
        /// Void transaction
        /// </summary>
        public void Void()
        {
            _requestParams.Add("MASTER_ID", MasterId);

            Post20API("VOID");
        }

        /// <summary>
        /// Rebill transaction
        /// </summary>
        public void SaleRecurring()
        {
            _requestParams.Add("DO_REBILL", DoRebill);
            _requestParams.Add("REB_FIRST_DATE", RebillFirstDate);
            _requestParams.Add("REB_EXPR", RebillExpression);
            _requestParams.Add("REB_CYCLES", RebillCycles);
            _requestParams.Add("REB_AMOUNT", RebillAmount);

            Sale(true);
        }

        /// <summary>
        /// Cancel rebill transaction
        /// </summary>
        public void CancelRecurring()
        {
            //check rebill
            _requestParams.Add("ACCOUNT_ID", AccountId);
            _requestParams.Add("USER_ID", UserId);
            _requestParams.Add("TRANS_TYPE", "GET");
            _requestParams.Add("REBILL_ID", MasterId);
            _requestParams.Add("TAMPER_PROOF_SEAL", CalculateTPS(null, string.Format("{0}{1}{2}{3}",
                SecretKey, AccountId, "GET", MasterId)));

            PostRequest(_requestParams.ToString(), "https://secure.bluepay.com/interfaces/bp20rebadmin");

            if (IsSuccessfulCancelRecurring)
                return;

            //stop rebilling if not deleted or already stopped
            _requestParams = HttpUtility.ParseQueryString(string.Empty);
            _requestParams.Add("ACCOUNT_ID", AccountId);
            _requestParams.Add("USER_ID", UserId);
            _requestParams.Add("TRANS_TYPE", "SET");
            _requestParams.Add("REBILL_ID", MasterId);
            _requestParams.Add("STATUS", "STOPPED");
            _requestParams.Add("TAMPER_PROOF_SEAL", CalculateTPS(null, string.Format("{0}{1}{2}{3}", 
                SecretKey, AccountId, "SET", MasterId)));
            
            PostRequest(_requestParams.ToString(), "https://secure.bluepay.com/interfaces/bp20rebadmin");
        }

        /// <summary>
        /// Set common parameters and post request to BluePay 2.0 API
        /// </summary>
        /// <param name="transactionType">Transaction type</param>
        private void Post20API(string transactionType)
        {
            _requestParams.Add("ACCOUNT_ID", AccountId);
            _requestParams.Add("USER_ID", UserId);
            _requestParams.Add("MODE", IsSandbox ? "TEST" : "LIVE");
            _requestParams.Add("TRANS_TYPE", transactionType);
            _requestParams.Add("VERSION", "3");

            _requestParams.Add("TPS_DEF", "ACCOUNT_ID MODE TRANS_TYPE AMOUNT MASTER_ID");
            _requestParams.Add("TAMPER_PROOF_SEAL", CalculateTPS(transactionType));

            PostRequest(_requestParams.ToString());
        }

        /// <summary>
        /// Calculates TAMPER_PROOF_SEAL 
        /// </summary>
        /// <returns>A hex-encoded md5 checksum</returns>
        private string CalculateTPS(string transactionType, string tamperProofSeal = null)
        {
            string tps = tamperProofSeal ?? string.Format("{0}{1}{2}{3}{4}{5}", 
                SecretKey, AccountId, IsSandbox ? "TEST" : "LIVE", transactionType, Amount, MasterId); 
            var md5 = new MD5CryptoServiceProvider();
            var hash = md5.ComputeHash(Encoding.Default.GetBytes(tps));
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }

        /// <summary>
        /// Check for rebill post stamp is genuine
        /// </summary>
        /// <param name="parameters">Post parameters</param>
        /// <returns>True, if genuine, otherwise false</returns>
        public bool CheckRebillStamp(NameValueCollection parameters)
        {
            var tamperProofSeal = parameters["BP_STAMP_DEF"].Split(' ')
                .Aggregate(SecretKey, (current, next) => current + parameters[next]);

            return string.Equals(parameters["BP_STAMP"], CalculateTPS(null, tamperProofSeal));
        }

        /// <summary>
        /// Get authorization id of template transaction for recurring payments
        /// </summary>
        /// <param name="id">Rebill id</param>
        /// <returns>Authorization id</returns>
        public string GetAuthorizationIdByRebillId(string id)
        {
            _requestParams.Add("ACCOUNT_ID", AccountId);
            _requestParams.Add("USER_ID", UserId);
            _requestParams.Add("TRANS_TYPE", "GET");
            _requestParams.Add("REBILL_ID", id);
            _requestParams.Add("TAMPER_PROOF_SEAL", CalculateTPS(null, string.Format("{0}{1}{2}{3}",
                SecretKey, AccountId, "GET", id)));

            PostRequest(_requestParams.ToString(), "https://secure.bluepay.com/interfaces/bp20rebadmin");

            return _responseParams["TEMPLATE_ID"] ?? string.Empty;
        }

        /// <summary>
        /// Send POST request to BluePay
        /// </summary>
        /// <param name="parameters">Request parameters</param>
        /// <param name="url">URL fo request</param>
        private void PostRequest(string parameters, string url = null)
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            byte[] postData = Encoding.Default.GetBytes(parameters);

            var request = (HttpWebRequest)WebRequest.Create(url ?? "https://secure.bluepay.com/interfaces/bp20post");
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = postData.Length;
            request.UserAgent = "nopCommerce";
            
            try
            {
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(postData, 0, postData.Length);
                }
                var httpResponse = (HttpWebResponse)request.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    _responseParams = HttpUtility.ParseQueryString(streamReader.ReadToEnd());
                }
            }
            catch (WebException e)
            {
                using (var streamReader = new StreamReader(e.Response.GetResponseStream()))
                {
                    _responseParams = HttpUtility.ParseQueryString(streamReader.ReadToEnd());
                    if (string.IsNullOrEmpty(Message))
                        _responseParams["MESSAGE"] = e.Message;
                }
            }
        }
    }
}
