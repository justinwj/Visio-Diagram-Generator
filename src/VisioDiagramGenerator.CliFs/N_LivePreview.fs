namespace VisioDiagramGenerator.CliFs

open System
open System.IO
open System.Threading.Tasks
open Azure.Identity
open Microsoft.Graph

/// <summary>
/// Module providing functionality to upload a VSDX diagram to the user's OneDrive via Microsoft Graph.
/// The upload uses device code authentication and returns a shareable link to the uploaded file. The
/// client and tenant identifiers are read from environment variables VDG_GRAPH_CLIENT_ID and
/// VDG_GRAPH_TENANT_ID. If these variables are missing, an exception is thrown.
/// </summary>
module LivePreview =

    /// Asynchronously uploads the specified VSDX file to the root of the current user's OneDrive and
    /// returns a URL that can be shared within the organisation. In future revisions this function
    /// could be extended to upload to a specific SharePoint site or folder as configured via
    /// environment variables.
    let uploadLivePreview (vsdxPath: string) : Task<string> =
        task {
            if String.IsNullOrWhiteSpace(vsdxPath) then
                invalidArg (nameof vsdxPath) "Path must be provided"

            let clientId = Environment.GetEnvironmentVariable("VDG_GRAPH_CLIENT_ID")
            let tenantId = Environment.GetEnvironmentVariable("VDG_GRAPH_TENANT_ID")
            if String.IsNullOrWhiteSpace(clientId) || String.IsNullOrWhiteSpace(tenantId) then
                invalidOp "VDG_GRAPH_CLIENT_ID and VDG_GRAPH_TENANT_ID environment variables must be set."

            let scopes = [| "Files.ReadWrite.All"; "Sites.ReadWrite.All" |]
            let options = DeviceCodeCredentialOptions()
            options.ClientId <- clientId
            options.TenantId <- tenantId
            options.DeviceCodeCallback <- (fun ctx -> Console.WriteLine(ctx.Message); Task.CompletedTask)
            let credential = new DeviceCodeCredential(options)
            let graphClient = new GraphServiceClient(credential, scopes)

            use stream = File.OpenRead(vsdxPath)
            let fileName = Path.GetFileName(vsdxPath)
            // Upload to OneDrive root
            let! driveItem = graphClient.Me.Drive.Root.ItemWithPath(fileName).Content.PutAsync(stream)
            // Create a share link for organisation-wide access
            let! permission = graphClient.Me.Drive.Items[driveItem.Id].CreateLink("view").Request().PostAsync()
            return permission.Link.WebUrl
        }