using System;
using System.IO;
using Murim.Core.Data;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public static class TestPaths
    {
        static GameData cached;

        public static string DataDir
        {
            get
            {
                foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Environment.CurrentDirectory })
                {
                    var dir = new DirectoryInfo(start);
                    while (dir != null)
                    {
                        var candidate = Path.Combine(dir.FullName, "Assets", "StreamingAssets", "Data");
                        if (Directory.Exists(candidate)) return candidate;
                        dir = dir.Parent;
                    }
                }
                throw new DirectoryNotFoundException("Assets/StreamingAssets/Data를 찾을 수 없습니다");
            }
        }

        /// <summary>출하용 데이터. 테스트 간 공유하므로 수정하지 말 것.</summary>
        public static GameData LoadShippedData()
        {
            if (cached != null) return cached;
            var r = DataLoader.LoadFromDirectory(DataDir);
            if (!r.Ok) Assert.Fail(string.Join("\n", r.Errors));
            cached = r.Data;
            return cached;
        }
    }
}
