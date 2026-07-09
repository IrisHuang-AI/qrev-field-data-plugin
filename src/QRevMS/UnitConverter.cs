namespace QRevMS
{
     public class UnitConverter
     {
         public bool IsImperial { get; }

         public UnitConverter(bool imperialUnits)
         {
             IsImperial = imperialUnits;
         }

         public string GetDistanceUnitId()
         {
             return IsImperial ? "ft" : "m";
         }

         public string GetAreaUnitId()
         {
             return IsImperial ? "ft^2" : "m^2";
         }

         public string GetVelocityUnitId()
         {
             return IsImperial ? "ft/s" : "m/s";
         }

         public string GetDischargeUnitId()
         {
             return IsImperial ? "ft^3/s" : "m^3/s";
         }
     }
}
