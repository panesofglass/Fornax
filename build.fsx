#r "paket:
nuget Fake.Core.Target
nuget Fake.Core.ReleaseNotes
nuget Fake.IO.FileSystem
nuget Fake.DotNet.AssemblyInfoFile
nuget Fake.DotNet.Cli
nuget Fake.DotNet.MsBuild
nuget Fake.DotNet.Testing.Expecto //"
#load "./.fake/build.fsx/intellisense.fsx"
#if !FAKE
#r "Facades/netstandard"
#r "netstandard"
#endif

open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.DotNet.Testing
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.IO.FileSystemOperators

let project = "Fornax"
let summary = "Fornax is a static site generator using type safe F# DSL to define page templates"
let release = ReleaseNotes.load "CHANGELOG.md"
let buildDir  = "./temp/build/"
let appReferences = !!  "src/**/*.fsproj"
let releaseDir  = "./temp/release/"
let releaseBinDir = "./temp/release/bin/"
let releaseReferences = !! "src/**/Fornax.fsproj"

let templates = "./src/Fornax.Template"

let buildTestDir  = "./temp/build_test/"
let testReferences = !!  "test/**/*.fsproj"

let testExecutables = !! (buildTestDir + "*Tests.exe")


// Targets
Target.create "Clean" (fun _ ->
    Shell.cleanDirs [buildDir; releaseDir; releaseBinDir; buildTestDir]
)

let (|Fsproj|Csproj|Vbproj|) (projFileName:string) =
    match projFileName with
    | f when f.EndsWith("fsproj") -> Fsproj
    | f when f.EndsWith("csproj") -> Csproj
    | f when f.EndsWith("vbproj") -> Vbproj
    | _                           -> failwith (sprintf "Project file %s not supported. Unknown project type." projFileName)

// Generate assembly info files with the right version & up-to-date information
Target.create "AssemblyInfo" (fun _ ->
    let getAssemblyInfoAttributes projectName =
        [ AssemblyInfo.Title (projectName)
          AssemblyInfo.Product project
          AssemblyInfo.Description summary
          AssemblyInfo.Version release.AssemblyVersion
          AssemblyInfo.FileVersion release.AssemblyVersion ]

    let getProjectDetails projectPath =
        let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
        ( projectPath,
          projectName,
          System.IO.Path.GetDirectoryName(projectPath),
          (getAssemblyInfoAttributes projectName)
        )

    appReferences
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (projFileName, _projectName, folderName, attributes) ->
        match projFileName with
        | Fsproj -> AssemblyInfoFile.createFSharp (folderName @@ "AssemblyInfo.fs") attributes
        | Csproj -> AssemblyInfoFile.createCSharp ((folderName @@ "Properties") @@ "AssemblyInfo.cs") attributes
        | Vbproj -> AssemblyInfoFile.createVisualBasic ((folderName @@ "My Project") @@ "AssemblyInfo.vb") attributes
        )
)


Target.create "Build" (fun _ ->
    appReferences
    |> Seq.iter (DotNet.build (fun p ->
        { p with
            Configuration=DotNet.BuildConfiguration.Debug
            OutputPath=Some buildDir
        }))
)

Target.create "BuildTest" (fun _ ->
    testReferences
    |> Seq.iter (DotNet.build (fun p ->
        { p with
            Configuration=DotNet.BuildConfiguration.Debug
            OutputPath=Some buildTestDir
        }))
)

Target.create "RunTest" (fun _ ->
    testExecutables
    |> Expecto.run id
)

Target.create "BuildRelease" (fun _ ->
    releaseReferences
    |> Seq.iter (DotNet.build (fun p ->
        { p with
            Configuration=DotNet.BuildConfiguration.Release
            OutputPath=Some releaseDir
        }))

    !! (releaseDir + "*.xml")
    ++ (releaseDir + "*.pdb")
    |> File.deleteAll

    !! (releaseDir + "*.dll")
    |> Seq.iter (Shell.moveFile releaseBinDir)
    let projectTemplateDir = releaseDir </> "templates" </> "project"

    Shell.copyDir projectTemplateDir templates (fun _ -> true)
)

// Build order
"Clean"
    ==> "AssemblyInfo"
    ==> "Build"
    ==> "BuildTest"
    ==> "RunTest"
    ==> "BuildRelease"

// start build
Target.runOrDefault "BuildRelease"
