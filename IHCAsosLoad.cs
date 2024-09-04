using System;
using System.Linq;
using System.Threading.Tasks;
using IHCHelixAzureProject.IHCClient;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IHCHelixAzureProject
{
    public class IHCAsosLoad
    {
        private readonly ILogger<IHCAsosLoad> _logger;

        public IHCAsosLoad(ILogger<IHCAsosLoad> logger)
        {
            _logger = logger;
        }

        [Function("IHCAsosLoad")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string user = req.Query["login"];
            string password = req.Query["password"];
            string endpoint = req.Query["endpoint"];
            if (!DateTime.TryParse(req.Query["start"], out var start))
            {
                return new BadRequestObjectResult("Start date is invalid!");
            }
            if (!DateTime.TryParse(req.Query["end"], out var end))
            {
                return new BadRequestObjectResult("End date is invalid!");
            }
            var aut=new AuthSoapClient(AuthSoapClient.EndpointConfiguration.AuthSoap12);
            var error = string.Empty;
            
            var authenticationResponse = await aut.LoginAsync(user, password);
            if (String.IsNullOrEmpty(authenticationResponse.error))
            {
                var request = new AsosSoapClient(AsosSoapClient.EndpointConfiguration.AsosSoap);
                var result=await request.GetASOsAsync(start, end, authenticationResponse.authKey);
                if (string.IsNullOrEmpty(result.error))
                {

                    //return new JsonResult(result.result.Where(x=>x.Patient=="JASON EDWARD ROBERT LEE"));

                    var asos = result.result.Where(r => r.dtLastASO.HasValue).Select(r => new Aso()
                    {
                        Id = ExtractCPF(r),
                        Data = r.dtLastASO?.ToString("yyyy-MM-dd"),
                        Local = r.Location,
                        Responsavel = r.DoctorName,
                        Tipo = TipoAso(r.ASOType),
                        Apto = r.Result?.ToLower() == "apto" ? "1" : "0",
                        Trabalhador = r.Patient,
                        OriginalType = r.ASOType
                    
                    
                    }).GroupBy(p => new {p.Id, p.Trabalhador, p.Tipo, p.Data}).Select(x => x.First()).ToList();
                    return new JsonResult(asos);

                }

                error = result.error;
            }
            else
            {
                error = authenticationResponse.error;
            }

            return new BadRequestObjectResult(error);            

        }
        
        private static string ExtractCPF(spOCCASOExportReportByFilter_Result r)
        {
            if (r.CPF == "0")
            {
                return string.Empty;
            }
            return r.CPF?.Replace(".", string.Empty).Replace("-", string.Empty)??string.Empty;
        }

        private string TipoAso(string asoType)
        {
            switch (asoType.ToLower())
            {
                case "periódico":
                    return "4"; 
                case "admissional":
                    return "1"; 
                case "retorno ao trabalho":
                    return "5";
                case "demissional":
                    return "2";
                case "mudança de função":
                    return "3";
                default:
                    return "0"; //Outros
            }
        }        
    }
}
