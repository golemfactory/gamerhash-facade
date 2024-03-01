using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Avalonia.Data;
using Avalonia.Data.Converters;

using GolemLib.Types;

using Nethereum.Signer;
using Nethereum.Util;

using Newtonsoft.Json;

using SHA3.Net;

namespace MockGUI.View
{
    public class BytesListConverter : IValueConverter
    {
        public static readonly BytesListConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null
                && value.GetType() == typeof(List<byte>)
                && parameter is string target
                && targetType.IsAssignableTo(typeof(string)))
            {
                var list = (List<byte>)value;
                switch (target)
                {
                    case "hex":
                        return "0x" + System.Convert.ToHexString(list.ToArray());
                    case "string":
                        return System.Text.Encoding.UTF8.GetString(list.ToArray());
                    case "json":
                        var jsonString = System.Text.Encoding.UTF8.GetString(list.ToArray());
                        return JsonConvert.SerializeObject(jsonString, Formatting.Indented);
                    default:
                        return "0x" + System.Convert.ToHexString(list.ToArray());
                }
            }

            // Converter used for the wrong type
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class SignatureConverter : IValueConverter
    {
        public static readonly SignatureConverter Instance = new();

        public EthECDSASignature ExtractEcdsaSignature(byte[] signatureArray)
        {
            var r = new byte[32];
            var s = new byte[32];

            var v = signatureArray[0];
            if ((v == 0) || (v == 1))
                v = (byte)(v + 27);
            Array.Copy(signatureArray, 1, r, 0, 32);
            Array.Copy(signatureArray, 33, s, 0, 32);

            var ecdaSignature = EthECDSASignatureFactory.FromComponents(r, s, v);
            return ecdaSignature;
        }

        private static byte[] Sha3_256(byte[] signedBytes)
        {
            using var hasher = Sha3.Sha3256();
            return hasher.ComputeHash(signedBytes);
        }

        private bool VerifySignature(byte[] signature, byte[] signedBytes, string address)
        {
            try
            {
                byte[] msgHash = Sha3_256(signedBytes);

                EthECDSASignature ethSignature = ExtractEcdsaSignature(signature);
                EthECKey key = EthECKey.RecoverFromSignature(ethSignature, msgHash);
                bool verified = key.Verify(msgHash, ethSignature);

                return verified && address == key.GetPublicAddress().ToLower();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private string RecoverNodeId(byte[] signature, byte[] signedBytes)
        {
            try
            {
                byte[] msgHash = Sha3_256(signedBytes);

                EthECDSASignature ethSignature = ExtractEcdsaSignature(signature);
                EthECKey key = EthECKey.RecoverFromSignature(ethSignature, msgHash);
                return key.GetPublicAddress();
            }
            catch (Exception e)
            {
                return $"Failed to recover: {e}";
            }
        }

        private string RecoverPubKey(byte[] signature, byte[] signedBytes)
        {
            try
            {
                byte[] msgHash = Sha3_256(signedBytes);

                EthECDSASignature ethSignature = ExtractEcdsaSignature(signature);
                EthECKey key = EthECKey.RecoverFromSignature(ethSignature, msgHash);
                return "0x" + System.Convert.ToHexString(key.GetPubKey());
            }
            catch (Exception e)
            {
                return $"Failed to recover: {e}";
            }
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not null and Payment && parameter is string target)
            {
                var payment = (Payment)value;
                if (payment.Signature == null || payment.SignedBytes == null)
                {
                    return "No signature";
                }

                try
                {
                    var signature = payment.Signature.ToArray();
                    var signed = payment.SignedBytes.ToArray();

                    return target switch
                    {
                        "RetrieveNodeId" => RecoverNodeId(signature, signed),
                        "RetrievePubKey" => RecoverPubKey(signature, signed),
                        "Validate" => VerifySignature(signature, signed, payment.PayerId) ? "true" : "false",
                        _ => VerifySignature(signature, signed, payment.PayerId) ? "true" : "false",
                    };
                }
                catch (Exception e)
                {
                    return $"Failed: {e}";
                }
            }
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }


        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
