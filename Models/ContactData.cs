using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formations.Models
{
    public class ContactData
    {
        public string? Nom { get; set; }
        public string? Prenom { get; set; }
        public string? Email { get; set; }
        public string? Telephone { get; set; }
        public string? Entreprise { get; set; }
        public string? Poste { get; set; }
        public string? Connaissance { get; set; }
        public string? ReturnUrl { get; set; }
        public string? Action { get; set; }
        public string? Cke { get; set; }
    }

    public class ContactRequest
    {
        public ContactData? Contact { get; set; }
    }

}
