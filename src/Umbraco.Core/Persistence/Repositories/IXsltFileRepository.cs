﻿using System.IO;
using Umbraco.Core.Models;

namespace Umbraco.Core.Persistence.Repositories
{
    public interface IXsltFileRepository : IReadRepository<string, XsltFile>, IWriteRepository<XsltFile>
    {
        bool ValidateXsltFile(XsltFile xsltFile);
        Stream GetFileContentStream(string filepath);
        void SetFileContent(string filepath, Stream content);
        long GetFileSize(string filepath);
    }
}
