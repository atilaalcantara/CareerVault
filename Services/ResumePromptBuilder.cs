using System.Text.Json;
using CareerVault.Api.Models;

namespace CareerVault.Api.Services;

public static class ResumePromptBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string BuildJobAnalysisPrompt(string jobDescription, string targetLanguage)
    {
        return $@"Voce e um especialista em analise de vagas e alinhamento de curriculo.

Idioma de saida: {targetLanguage}

Tarefa:
- Ler a descricao bruta da vaga abaixo.
- Extrair cargo alvo, senioridade, hard skills obrigatorias, desejaveis, palavras-chave de dominio e principais responsabilidades.
- Gerar entre 4 e 8 queries curtas e objetivas para consultar uma base vetorial de experiencias profissionais.
- As queries devem ser diversificadas, cobrindo stack, dominio, responsabilidades e contexto.
- Nao gere HTML.
- Nao escreva explicacoes fora do JSON.
- Responda com JSON estrito no formato:
{{
  ""targetRole"": ""string"",
  ""seniority"": ""string"",
  ""mustHaveSkills"": [""string""],
  ""niceToHaveSkills"": [""string""],
  ""domainKeywords"": [""string""],
  ""responsibilities"": [""string""],
  ""searchQueries"": [""string""]
}}

Descricao da vaga:
{jobDescription}";
    }

    public static string BuildResumeDraftPrompt(ResumeGenerationContext context)
    {
        var payload = new
        {
            profile = context.Profile,
            jobDescription = context.JobDescription,
            templateId = context.TemplateId,
            targetLanguage = context.TargetLanguage,
            jobAnalysis = context.JobAnalysis,
            evidence = context.Evidence
        };

        return $@"Voce e um especialista em curriculos ATS-friendly e adaptacao de curriculo por vaga.

Regras obrigatorias:
- Use apenas as evidencias fornecidas.
- Considere que dados de identidade, links e formacao academica fixa ja sao fornecidos pelo perfil estatico da aplicacao.
- Nao invente experiencias, tecnologias, empresas, resultados ou formacoes.
- Escreva de forma objetiva, profissional e clara.
- Priorize aderencia real a vaga.
- Prefira bullets curtos e fortes.
- Se uma informacao nao estiver nas evidencias, omita.
- Nao transforme conhecimento conceitual, estudo superficial ou contato indireto em experiencia pratica.
- Nao promova tecnologia para skill principal se ela nao aparecer de forma clara e recorrente nas evidencias.
- Nao inferira ASP.NET MVC, testes unitarios, Kafka, RabbitMQ, React ou Angular como experiencia pratica sem evidencia direta.
- Diferencie experiencia pratica de estudo, formacao academica e certificacoes.
- So preencha educationItems se houver educacao adicional relevante alem da formacao fixa do perfil ou se precisar complementar detalhes nao presentes no perfil.
- Em vaga junior, seja conservador: prefira honestidade e aderencia parcial real a parecer mais senior do que a base permite.
- Nao gere HTML.
- Nao escreva texto fora do JSON.

Gere JSON estrito no formato:
{{
  ""headline"": ""string"",
  ""professionalSummary"": ""string"",
  ""coreSkills"": [""string""],
  ""experienceItems"": [
    {{
      ""title"": ""string"",
      ""company"": ""string"",
      ""project"": ""string"",
      ""period"": ""string"",
      ""bullets"": [""string""]
    }}
  ],
  ""educationItems"": [
    {{
      ""institution"": ""string"",
      ""degree"": ""string"",
      ""details"": ""string""
    }}
  ],
  ""certificationItems"": [
    {{
      ""title"": ""string"",
      ""details"": ""string""
    }}
  ],
  ""projectItems"": [
    {{
      ""title"": ""string"",
      ""details"": ""string""
    }}
  ],
  ""keywordCoverage"": [""string""]
}}

Contexto completo:
{JsonSerializer.Serialize(payload, JsonOptions)}";
    }
}
