﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Golem.Model
{
   public class YagnaAgreement
   {
      public class YagnaAgreementDemand
      {
         public Dictionary<string, object>? Properties { get; set; }
         public string? Constraints { get; set; }
         public string? DemandID { get; set; }
         public DateTime? Timestamp { get; set; }
      }
      public class YagnaAgreementOffer
      {
         public Dictionary<string, object>? Properties { get; set; }
         public string? Constraints { get; set; }
         public string? OfferID { get; set; }
         public string? ProviderID { get; set; }
         public DateTime? Timestamp { get; set; }
      }
      public string? AgreementID { get; set; }

      public YagnaAgreementOffer? Offer { get; set; }
      public YagnaAgreementDemand? Demand { get; set; }

      public DateTime? ValidTo { get; set; }
      public DateTime? ApprovedDate { get; set; }
      public string? State { get; set; }
      public DateTime? Timestamp { get; set; }
      public string? AppSessionID { get; set; }
   }
}
