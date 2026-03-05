// …inside the try block of the step…
var response = await Http.Send(httpClient, request)
    .WaitAsync(TimeSpan.FromMilliseconds(cfg.TimeoutMs));

// …status‑code check omitted for brevity…

// read the body text
var httpResponse = response.Body;             // HttpResponseMessage
var bodyText     = await httpResponse.Content.ReadAsStringAsync();

var responseData = new Dictionary<string,string>();
if (cfg.ResponseHandler is not null)
{
    responseData = await ResponseHandlerService.ExtractResponseData(
                      bodyText, cfg.ResponseHandler);

    // …rest of your logging/condition logic…
}


// …inside BuildScenario…
var statePerCopy = new ConcurrentDictionary<int,Dictionary<string,string>>();

return Scenario.Create(scenarioName, async context =>
{
    var cfg = settings!;

    // get or create the mutable state for this copy/virtual user
    var copyState = statePerCopy.GetOrAdd(context.ScenarioInfo.CopyNumber,
                                            _ => new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase));

    // feed record
    var dataRecord = dataFeed?.GetNextItem(context.ScenarioInfo);

    // merge state + record for substitution
    var subs = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    if (dataRecord != null)
    {
        foreach (var kv in dataRecord) subs[kv.Key] = kv.Value;
    }
    foreach (var kv in copyState) subs[kv.Key] = kv.Value;

    var url = ApplySubstitutions(cfg.Url, subs);
    var stepName = cfg.StepName ?? $"{cfg.HttpMethod} {cfg.Url}";

    var step = await Step.Run(stepName, context, async () =>
    {
        // …build request as before…

        try
        {
            var response = await Http.Send(httpClient, request)
                .WaitAsync(TimeSpan.FromMilliseconds(cfg.TimeoutMs));

            // …status‑code checks…

            // read the body text
            var httpResponse = response.Body;
            var bodyText     = await httpResponse.Content.ReadAsStringAsync();

            // extract and store response data
            if (cfg.ResponseHandler is not null)
            {
                var responseData = await ResponseHandlerService.ExtractResponseData(
                                        bodyText, cfg.ResponseHandler);

                // keep values for the next invocation
                foreach (var kv in responseData)
                    copyState[kv.Key] = kv.Value;

                var shouldContinue = ResponseHandlerService.EvaluateCondition(
                                         cfg.ResponseHandler.ContinueCondition,
                                         responseData);
                if (!shouldContinue)
                {
                    context.Logger.Information(
                        "Response handler condition not met. Stopping pagination. " +
                        "Scenario={Scenario} Invocation={Invocation}",
                        scenarioName, context.InvocationNumber);
                }

                // …logging as before…
            }

            // …return Response.Ok …
        }
        catch (Exception ex) when (ex is TimeoutException or TaskCanceledException or OperationCanceledException)
        {
            // …timeout handling …
        }
    });

    return step;
})
.WithInit(context =>
{
    // initialise copy‑state if you want a starting offset, etc.
    statePerCopy[context.ScenarioInfo.CopyNumber] = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
    {
        // optionally seed, e.g. { "Offset", "0" }
    };

    // …existing init code …
})
.WithClean(context =>
{
    statePerCopy.TryRemove(context.ScenarioInfo.CopyNumber, out _);
    // …existing clean …
});



public static async Task<Dictionary<string,string>> ExtractResponseData(
    HttpResponseMessage httpResponse,
    ResponseHandlerSettings handler)
{
    var body = await httpResponse.Content.ReadAsStringAsync();
    return ExtractResponseDataFromString(body, handler);
}

public static Task<Dictionary<string,string>> ExtractResponseData(
    string responseBody,
    ResponseHandlerSettings handler)
{
    // previous implementation (deserialize, extract by path, etc.)
    …
}


