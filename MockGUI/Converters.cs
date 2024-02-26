using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Avalonia.Data;
using Avalonia.Data.Converters;

using CommandLine;

using GolemLib.Types;

using Nethereum.Signer;
using Nethereum.Util;

using Newtonsoft.Json;

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

    public class SignatureVerificationConverter : IValueConverter
    {
        public static readonly SignatureVerificationConverter Instance = new();

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

        private bool VerifySignature(byte[] signature, byte[] signedBytes)
        {
            byte[] msgHash = new Sha3Keccack().CalculateHash(signedBytes);

            EthECDSASignature ethSignature = ExtractEcdsaSignature(signature);
            EthECKey key = EthECKey.RecoverFromSignature(ethSignature, msgHash);
            bool verified = key.Verify(msgHash, ethSignature);

            return verified;
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not null and Payment)
            {
                var payment = (Payment)value;

                return payment.Signature != null && payment.SignedBytes != null
                    ? VerifySignature(payment.Signature.ToArray(), payment.SignedBytes.ToArray()) ? "true" : "false"
                    : (object)false;
            }

            // Converter used for the wrong type
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }


        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
