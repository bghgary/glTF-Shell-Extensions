using System.Diagnostics;
using System.Reflection;

namespace glTF
{
    [TestClass]
    public sealed class Core(TestContext testContext)
    {
        private static readonly string ValidatorExePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "gltf_validator.exe");

        private readonly CancellationToken cancellationToken = testContext.CancellationTokenSource.Token;
        private readonly string resultsDirectoryPath = Path.Combine(testContext.TestRunDirectory!, testContext.TestName!);
        private readonly string modelsDirectoryPath = Path.GetFullPath(Path.Combine(testContext.TestRunDirectory!, @"..\..\..\..\Source\Tests\Assets"));

        public async Task ValidateAsync(string filePath)
        {
            using var process = Process.Start(new ProcessStartInfo(ValidatorExePath, [filePath])
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            })!;

            await process.WaitForExitAsync(this.cancellationToken);
            if (process.ExitCode != 0)
            {
                throw new Exception("Validation failed");
            }
        }

        private string Pack(string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var inputFilePath = Path.Combine(this.modelsDirectoryPath, filePath);
            var outputFilePath = Path.Combine(this.resultsDirectoryPath, $"{fileName}.glb");
            Packer.Pack(inputFilePath, outputFilePath);
            return outputFilePath;
        }

        private string Unpack(string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var inputFilePath = Path.Combine(this.modelsDirectoryPath, filePath);
            var outputDirectoryPath = Path.Combine(this.resultsDirectoryPath, fileName);
            Unpacker.Unpack(inputFilePath, outputDirectoryPath, true);
            return Path.Combine(outputDirectoryPath, $"{fileName}.gltf");
        }

        [TestMethod]
        public async Task Pack_Box()
        {
            var outputFilePath = this.Pack(@"Box\glTF\Box.gltf");
            await this.ValidateAsync(outputFilePath);
        }

        [TestMethod]
        public async Task Pack_Box_Embedded()
        {
            var outputFilePath = this.Pack(@"Box\glTF-Embedded\Box.gltf");
            await this.ValidateAsync(outputFilePath);
        }

        [TestMethod]
        public async Task Pack_BoxTextured()
        {
            var outputFilePath = this.Pack(@"BoxTextured\glTF\BoxTextured.gltf");
            await this.ValidateAsync(outputFilePath);
        }

        [TestMethod]
        public async Task Unpack_Box()
        {
            var outputFilePath = this.Unpack(@"Box\glTF-Binary\Box.glb");
            await this.ValidateAsync(outputFilePath);
        }

        [TestMethod]
        public async Task Unpack_BoxTextured()
        {
            var outputFilePath = this.Unpack(@"BoxTextured\glTF-Binary\BoxTextured.glb");
            await this.ValidateAsync(outputFilePath);
        }

        [TestMethod]
        public async Task Pack_Unpack_BoxTextured_Embedded()
        {
            var outputFilePath = this.Unpack(this.Pack(@"BoxTextured\glTF-Embedded\BoxTextured.gltf"));
            await this.ValidateAsync(outputFilePath);
        }

        [TestMethod]
        public async Task Pack_Unpack_LightsIES()
        {
            string[] filePaths = [@"LightsIES\Example.gltf", @"LightsIES\Example_bufferview.gltf", @"LightsIES\Example_dataUri.gltf"];
            foreach (var filePath in filePaths)
            {
                var outputFilePath = this.Unpack(this.Pack(filePath));
                await this.ValidateAsync(outputFilePath);
            }
        }
    }
}
