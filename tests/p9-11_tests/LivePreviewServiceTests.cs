using System;
using System.IO;
using System.Text.RegularExpressions;
using VDG.Core.Preview;
using Xunit;

namespace VDG.Tests.P9_11
{
    public class LivePreviewServiceTests
    {
        [Fact]
        public void CreatePreview_ReturnsUrl_And_WritesTempFile()
        {
            ILivePreviewService svc = new LivePreviewServiceStub();
            string url = svc.CreatePreview(suggestedName: "TestPreview.vsdx");

            Assert.Matches(new Regex("^https?://"), url);

            // Decode id to get temp path (inverse of the stub encoding).
            string id = url.Substring(url.LastIndexOf('/') + 1);
            string padded = id.Replace('-', '+').Replace('_', '/').PadRight(((id.Length + 3) / 4) * 4, '=');
            string tempPath = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            Assert.True(File.Exists(tempPath), "Expected the stub to write a temp .vsdx file.");
        }
    }
}