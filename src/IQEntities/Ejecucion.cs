using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IQEntities
{
    public sealed class Ejecucion
    {
        public int Id { get; set; }
        public int ProcesoId { get; set; }
        public DateTime FechaHora { get; set; }
        public byte Estatus { get; set; }
    }

}
