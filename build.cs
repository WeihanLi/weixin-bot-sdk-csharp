#:package WeihanLi.Common@1.0.87

using WeihanLi.Common.Helpers;

var solutionPath = "./weixin-bot-sdk-csharp.slnx";
string[] srcProjects = [ 
    "./Weixin.Bot.Sdk/Weixin.Bot.Sdk.csproj"
];
string[] testProjects = [ 
    "./Weixin.Bot.Sdk.Test/Weixin.Bot.Sdk.Test.csproj"
];
string[] runFileSamplesFolders = [
    "./run-file-samples"
];

await DotNetPackageBuildProcess
    .Create(options => 
    {
        options.SolutionPath = solutionPath;
        options.SrcProjects = srcProjects;
        options.TestProjects = testProjects;
        options.RunFileSampleFolders = runFileSamplesFolders;
    })
    .ExecuteAsync(args);
