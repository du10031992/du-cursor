using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MepPanel.AutoCAD.Licensing
{
    public partial class CheckLicenseResponse
    {
        [DataMember(Name = "features")]
        public List<string> Features { get; set; }
    }
}
