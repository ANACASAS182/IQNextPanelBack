using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IQEntities
{
    public sealed class Empresa
    {
        public int Id { get; set; }
        public string Rfc { get; set; } = "";
        public string Nombre { get; set; } = "";
    }
}
