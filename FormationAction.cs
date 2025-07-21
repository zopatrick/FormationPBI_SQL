using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
//using Microsoft.Azure.WebJobs;
//using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System;
using System.Net.Http.Headers;
using Microsoft.Azure.Functions.Worker;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Text;
using Google.Protobuf.WellKnownTypes;
using Microsoft.VisualBasic;
using System.Reflection.Metadata.Ecma335;
using Formations.Models;

public class FormationActions
{
    // Constante value
    public const long pUnitAmount = 1; // prix en centimes (700€ TTC)
    public const string pCurrency = "eur";
    public const string pProductDataName = "Formation Power BI & SQL";


    [Function("subscription")]
    public static async Task<IActionResult> Run(
     [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, FunctionContext executionContext,
     ILogger log)
    {
        {
            try
            {
                if (log == null)
                    log = executionContext.GetLogger("subscription");

                log.LogInformation("Appel à FormationActions");
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<Formations.Models.ContactRequest>(requestBody);
                if (data == null || data.Contact == null)
                {
                    log.LogError("Le corps de la requête est vide ou mal formé.");
                    return new BadRequestObjectResult("Le corps de la requête est vide ou mal formé.");
                }

                // Securité: vérifier la clé API
                string? apiKey = data.Contact.Cke;
                if (apiKey != Environment.GetEnvironmentVariable("Azure_API_KEY"))
                {
                    return new UnauthorizedResult();
                }

                string? action = data.Contact.Action;
                // Extraire les données depuis le body JSON
                string? nom = data.Contact.Nom;
                string? prenom = data.Contact.Prenom;
                string? email = data.Contact.Email;
                string? telephone = data.Contact.Telephone;
                string? entreprise = data.Contact.Entreprise;
                string? poste = data.Contact.Poste;
                string? connaissance = data.Contact.Connaissance;
                string? returnUrl = data.Contact.ReturnUrl;
                //string? planId = data.Contact.planId;

                switch (action)
                {
                    case "brochure":
                        await CreateContact(email, nom, prenom, telephone, entreprise, poste, connaissance, log);
                        return await DownloadBrochure();

                    case "info":
                        return await CreateContact(email, nom, prenom, telephone, entreprise, poste, connaissance, log);

                    case "subscription":
                        await CreateContact(email, nom, prenom, telephone, entreprise, poste, connaissance, log);
                        return CallToStripe(email, returnUrl, log);

                    default:
                        return new BadRequestObjectResult("Action non reconnue.");
                }
            }
            catch (JsonException jsonEx)
            {
                log.LogError($"JsonException inattendue : {jsonEx.Message}");
                // Gérer les erreurs de désérialisation JSON
                return new BadRequestObjectResult($"Erreur de format JSON : {jsonEx.Message}");
            }
            catch (HttpRequestException httpEx)
            {
                log.LogError($"HttpRequestException inattendue : {httpEx.Message}");
                // Gérer les erreurs de requête HTTP
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
            catch (Exception ex)
            {
                log.LogError($"Exception inattendue : {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }

    private static async Task<IActionResult> DownloadBrochure()
    {
        var filePath = Path.Combine(Path.Combine(Environment.CurrentDirectory, "//Documents", "Brochure_Formation_PowerBI_SQL.pdf"));
        if (!File.Exists(filePath))
            return new NotFoundResult();

        var fileBytes = await File.ReadAllBytesAsync(filePath);
        return new FileContentResult(fileBytes, "application/pdf")
        {
            FileDownloadName = "Formation_PowerBI_SQL.pdf"
        };

    }
    private static async Task<IActionResult> CreateContact(string? email, string? nom, string? prenom, string? telephone, string? entreprise, string? poste, string? connaissance, ILogger log)
    {
        log.LogInformation("Création du contact dans Airtable");
        // Récupération des variables d'environnement
        string? airtableToken = Environment.GetEnvironmentVariable("AIRTABLE_BEARER");
        string? airtableBaseId = Environment.GetEnvironmentVariable("AIRTABLE_BASE_ID"); 
        string? airtableTableId = Environment.GetEnvironmentVariable("AIRTABLE_TABLE");

        if (string.IsNullOrEmpty(airtableToken)  || string.IsNullOrEmpty(airtableTableId))
        {
            return new BadRequestObjectResult("Configuration Airtable incomplète.");
        }

        // Configuration du client HTTP avec authentification
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", airtableToken);

        // Création de l'objet JSON à envoyer
        var airtablePayload = new
        {
            fields = new Dictionary<string, string>
                {
                    { "Nom", nom != null ? nom.ToString() : string.Empty },
                    { "Prénom", prenom != null ? prenom.ToString() : string.Empty },
                    { "Adresse e-mail", email != null ? email.ToString() : string.Empty },
                    { "Numéro de téléphone", telephone != null ? telephone.ToString() : string.Empty },
                    { "Entreprise", nom != null ? nom.ToString() : string.Empty },
                    { "Poste", poste != null ? poste.ToString() : string.Empty },
                    { "Source", connaissance != null ? connaissance.ToString() : string.Empty }
                }
        };

        var airtableContent = new StringContent(
            JsonConvert.SerializeObject(airtablePayload),
            Encoding.UTF8,
            "application/json"
        );

        // Appel à l'API Airtable
        var response = await httpClient.PostAsync($"https://api.airtable.com/v0/{airtableBaseId}/{airtableTableId}", airtableContent);

        // Retour de l'état de l'appel
        if (response.IsSuccessStatusCode)
        {
            return new OkObjectResult(new { success = true });
        }
        else
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            return new BadRequestObjectResult(new { success = false, error = errorBody });
        }
    }


    /// <summary>
    /// Check information about formation
    /// </summary>
    /// <param name="email"></param>
    /// <returns></returns>
    private static IActionResult CallToStripe(string? email, string? returnl, ILogger log)
    {
        log.LogInformation("Appel à Stripe pour créer une session de paiement");
        // appel Stripe pour créer une session
        string? stripeSecretKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
        Stripe.StripeConfiguration.ApiKey = stripeSecretKey;
        var options = new Stripe.Checkout.SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<Stripe.Checkout.SessionLineItemOptions>
                    {
                        new Stripe.Checkout.SessionLineItemOptions
                        {
                            Price = Environment.GetEnvironmentVariable("STRIPE_PriceId"),
                            Quantity = 1,
                        },
                    },
            Mode = "payment",
            SuccessUrl = "https://tonsite.com/success",
            CancelUrl = "https://tonsite.com/cancel",
            CustomerEmail = email
        };

        var service = new Stripe.Checkout.SessionService();
        Stripe.Checkout.Session session = service.Create(options);

        return new OkObjectResult(new { success = true, checkoutUrl = session.Url });
    }


    //[Function("downloadbrochure")]
    //public static async Task<IActionResult> HandleBrochure(
    //[HttpTrigger(AuthorizationLevel.Function, "get", Route = "telechargement")] HttpRequest req, FunctionContext executionContext,
    //ILogger log)
    //{
    //    if (log == null)
    //        log = executionContext.GetLogger("downloadBrochure");

    //    // Exemple : autorisation par clé ou condition
    //    string key = req.Query["cke"] != string.Empty ? req.Query["cke"].ToString() : string.Empty;
    //    if (key != Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY")) 
    //        return new UnauthorizedResult();
    //    var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Front/Documents", "Brochure_Formation_PowerBI_SQL.pdf");
    //    if (!File.Exists(filePath))
    //        return new NotFoundResult();

    //    var fileBytes = await File.ReadAllBytesAsync(filePath);
    //    return new FileContentResult(fileBytes, "application/pdf")
    //    {
    //        FileDownloadName = "Formation_PowerBI_SQL.pdf"
    //    };
    //}
}

