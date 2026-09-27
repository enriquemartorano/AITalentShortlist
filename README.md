# AI Talent Shortlist

Demo-mode backend for candidate shortlisting.

- In-memory storage only.
- Six fictional candidates can be loaded with `POST /api/demo/seed`.
- Deterministic keyword-based evaluation.
- All recommendations require human review.
- No Azure services, real CVs, or protected-characteristic evaluation.

## Azure demo settings

For Azure App Service, configure the OpenAI key as an Application Setting named `OpenAI__ApiKey`. App Service stores Application Settings as encrypted secrets and .NET maps the double underscore to `OpenAI:ApiKey`.

Example Azure CLI command (replace the placeholders and enter the key only in your local terminal):

```powershell
az webapp config appsettings set --resource-group <resource-group> --name <app-service-name> --settings OpenAI__ApiKey='<your-openai-key>'
```

The demo protects the evaluation endpoint with configurable hourly limits:

- `500` evaluations per IP per hour.
- `1000` evaluations globally per hour.

Override them with App Service settings `DemoLimits__EvaluationsPerIpPerHour` and `DemoLimits__GlobalEvaluationsPerHour`. Requests over a limit receive HTTP `429` and do not call OpenAI.
