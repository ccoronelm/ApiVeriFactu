using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace gesFactu.Domain.Enums
{
    public enum EstadosEnum
    {
        [EnumMember(Value = "Inactivo")]
        Inactivo,
        [EnumMember(Value = "Activo")]
        Activo
    }
}
