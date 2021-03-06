﻿/*
    Copyright (C) 2006-2018. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using Aardvark.Base;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ImportTests
    {
        #region File Formats

        [Test]
        public void CanRegisterFileFormat()
        {
            PointCloudFileFormat.Register(new PointCloudFileFormat("Test Description 1", new[] { ".test1" }, null, null));
        }

        [Test]
        public void CanRetrieveFileFormat()
        {
            PointCloudFileFormat.Register(new PointCloudFileFormat("Test Description 2", new[] { ".test2" }, null, null));

            var format = PointCloudFileFormat.FromFileName(@"C:\Data\pointcloud.test2");
            Assert.IsTrue(format != null);
            Assert.IsTrue(format.Description == "Test Description 2");
        }

        [Test]
        public void CanRetrieveFileFormat2()
        {
            PointCloudFileFormat.Register(new PointCloudFileFormat("Test Description 3", new[] { ".test3", ".tst3" }, null, null));

            var format1 = PointCloudFileFormat.FromFileName(@"C:\Data\pointcloud.test3");
            var format2 = PointCloudFileFormat.FromFileName(@"C:\Data\pointcloud.tst3");
            Assert.IsTrue(format1 != null && format1.Description == "Test Description 3");
            Assert.IsTrue(format2 != null && format2.Description == "Test Description 3");
        }

        [Test]
        public void UnknownFileFormatGivesUnknown()
        {
            var format = PointCloudFileFormat.FromFileName(@"C:\Data\pointcloud.foo");
            Assert.IsTrue(format == PointCloudFileFormat.Unknown);
        }

        #endregion

        #region Chunks

        [Test]
        public void CanImportChunkWithoutColor()
        {
            int n = 100;
            var r = new Random();
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble());
            var chunk = new Chunk(ps);

            Assert.IsTrue(chunk.Count == 100);

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey("test")
                .WithOctreeSplitLimit(10)
                ;

            var pointcloud = PointCloud.Chunks(chunk, config);
            Assert.IsTrue(pointcloud.PointCount == 100);
        }

        [Test]
        public void CanImportChunk_MinDist()
        {
            int n = 100;
            var r = new Random();
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble());
            var chunk = new Chunk(ps);

            Assert.IsTrue(chunk.Count == 100);

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey("test")
                .WithOctreeSplitLimit(10)
                .WithMinDist(0.5)
                ;
            var pointcloud = PointCloud.Chunks(chunk, config);
            Assert.IsTrue(pointcloud.PointCount < 100);
        }

        [Test]
        public void CanImportChunk_Reproject()
        {
            int n = 10;
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(i, 0, 0);
            var bb = new Box3d(ps);

            var chunk = new Chunk(ps);
            Assert.IsTrue(chunk.Count == 10);
            Assert.IsTrue(chunk.BoundingBox == bb);

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey("test")
                .WithOctreeSplitLimit(10)
                .WithReproject(xs => xs.Select(x => x += V3d.OIO).ToArray())
                ;
            var pointcloud = PointCloud.Chunks(chunk, config);
            Assert.IsTrue(pointcloud.BoundingBox == bb + V3d.OIO);
        }

        [Test]
        public void CanImportChunk_EstimateNormals()
        {
            int n = 10;
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(i, 0, 0);

            var chunk = new Chunk(ps);
            Assert.IsTrue(chunk.Count == 10);

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey("test")
                .WithOctreeSplitLimit(10)
                ;
            var pointcloud = PointCloud.Chunks(chunk, config);
            var node = pointcloud.Root.Value;
            Assert.IsTrue(node.IsLeaf);
            Assert.IsTrue(node.HasNormals == false);


            config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey("test")
                .WithOctreeSplitLimit(10)
                .WithEstimateNormals(xs => xs.Select(x => V3f.OOI).ToArray())
                ;
            pointcloud = PointCloud.Chunks(chunk, config);
            node = pointcloud.Root.Value;
            Assert.IsTrue(node.IsLeaf);
            Assert.IsTrue(node.HasNormals == true);
            Assert.IsTrue(node.Normals.Value.All(x => x == V3f.OOI));
        }

        [Test]
        public void CanImport_WithKey()
        {
            int n = 10;
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(i, 0, 0);

            var chunk = new Chunk(ps);
            Assert.IsTrue(chunk.Count == 10);

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey("test")
                .WithOctreeSplitLimit(10)
                .WithCreateOctreeLod(false)
                .WithDeduplicateChunks(false)
                .WithMinDist(0.0)
                .WithReproject(null)
                .WithEstimateNormals(null)
                ;
            var pointcloud = PointCloud.Chunks(chunk, config);
            Assert.IsTrue(pointcloud.Id == "test");
        }

        [Test]
        public void CanImport_WithoutKey()
        {
            int n = 10;
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(i, 0, 0);

            var chunk = new Chunk(ps);
            Assert.IsTrue(chunk.Count == 10);

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey(null)
                .WithOctreeSplitLimit(10)
                .WithCreateOctreeLod(false)
                .WithDeduplicateChunks(false)
                .WithMinDist(0.0)
                .WithReproject(null)
                .WithEstimateNormals(null)
                ;
            var pointcloud = PointCloud.Chunks(chunk, config);
            Assert.IsTrue(pointcloud.Id != null);
        }

        [Test]
        public void CanImport_DuplicateKey()
        {
            int n = 10;
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(i, 0, 0);

            var chunk = new Chunk(ps);
            Assert.IsTrue(chunk.Count == 10);

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey("test")
                .WithOctreeSplitLimit(10)
                .WithCreateOctreeLod(false)
                .WithDeduplicateChunks(false)
                .WithMinDist(0.0)
                .WithReproject(null)
                .WithEstimateNormals(null)
                ;


            var pointcloud = PointCloud.Chunks(new Chunk[] { }, config);
            Assert.IsTrue(pointcloud.Id != null);
            Assert.IsTrue(pointcloud.PointCount == 0);


            var pointcloud2 = PointCloud.Chunks(chunk, config);
            Assert.IsTrue(pointcloud2.Id != null);
            Assert.IsTrue(pointcloud2.PointCount == 10);


            var reloaded = config.Storage.GetPointSet("test", CancellationToken.None);
            Assert.IsTrue(reloaded.PointCount == 10);
        }

        [Test]
        public void CanImport_Empty()
        {
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey("test")
                .WithOctreeSplitLimit(10)
                .WithCreateOctreeLod(false)
                .WithDeduplicateChunks(false)
                .WithMinDist(0.0)
                .WithReproject(null)
                .WithEstimateNormals(null)
                ;


            var pointcloud = PointCloud.Chunks(new Chunk[] { }, config);
            Assert.IsTrue(pointcloud.Id == "test");
            Assert.IsTrue(pointcloud.PointCount == 0);
            
            var reloaded = config.Storage.GetPointSet("test", CancellationToken.None);
            Assert.IsTrue(reloaded.Id == "test");
            Assert.IsTrue(reloaded.PointCount == 0);
        }

        #endregion

        #region General

        [Test]
        public void CanCreateInMemoryStore()
        {
            var store = PointCloud.CreateInMemoryStore();
            Assert.IsTrue(store != null);
        }

        [Test]
        public void CanCreateOutOfCoreStore()
        {
            var storepath = Path.Combine(Config.TempDataDir, Guid.NewGuid().ToString());
            var store = PointCloud.OpenStore(storepath);
            Assert.IsTrue(store != null);
        }

        [Test]
        public void CanCreateOutOfCoreStore_FailsIfPathIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var storepath = (string)null;
                var store = PointCloud.OpenStore(storepath);
                Assert.IsTrue(store != null);
            });
        }

        [Test]
        public void CanCreateOutOfCoreStore_FailsIfInvalidPath()
        {
            Assert.That(() =>
            {
                var storepath = @"some invalid path C:\";
                var store = PointCloud.OpenStore(storepath);
                Assert.IsTrue(store != null);
            },
            Throws.Exception
            );
        }

        [Test]
        public void CanImportFile_WithoutConfig_InMemory()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            TestContext.WriteLine($"testfile is '{filename}'");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            var a = PointCloud.Import(filename);
            Assert.IsTrue(a != null);
            Assert.IsTrue(a.PointCount == 3);
        }

        [Test]
        public void CanImportFile_WithoutConfig_OutOfCore()
        {
            var storepath = Path.Combine(Config.TempDataDir, Guid.NewGuid().ToString());
            TestContext.WriteLine($"storepath is '{storepath}'");
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var a = PointCloud.Import(filename, storepath);
            Assert.IsTrue(a != null);
            Assert.IsTrue(a.PointCount == 3);
        }

        [Test]
        public void CanImportFileAndLoadFromStore()
        {
            var storepath = Path.Combine(Config.TempDataDir, Guid.NewGuid().ToString());
            TestContext.WriteLine($"storepath is '{storepath}'");
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var a = PointCloud.Import(filename, storepath);
            var key = a.Id;

            var b = PointCloud.Load(key, storepath);
            Assert.IsTrue(b != null);
            Assert.IsTrue(b.PointCount == 3);
        }

        #endregion

        #region Pts

        [Test]
        public void CanParsePtsFileInfo()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var info = PointCloud.ParseFileInfo(filename, ImportConfig.Default);

            Assert.IsTrue(info.PointCount == 3);
            Assert.IsTrue(info.Bounds == new Box3d(new V3d(1), new V3d(9)));
        }

        [Test]
        public void CanParsePtsFile()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            var ps = PointCloud.Parse(filename, ImportConfig.Default)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(ps.Length == 3);
            Assert.IsTrue(ps[0].ApproxEqual(new V3d(1, 2, 9), 1e-10));
        }
        
        [Test]
        public void CanImportPtsFile()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey("test")
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset != null);
            Assert.IsTrue(pointset.PointCount == 3);
        }

        [Test]
        public void CanImportPtsFile_MinDist()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey("test")
                .WithMinDist(10.0);
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset.PointCount < 3);
        }

        [Test]
        public void CanImportPtsFileAndLoadFromStore()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey("test")
                ;
            var pointset = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test", CancellationToken.None);
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 3);
        }

        [Test]
        public void CanImportPtsFileAndLoadFromStore_CheckKey()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey("test")
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset.Id == "test");
            var pointset2 = config.Storage.GetPointSet("test", CancellationToken.None);
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 3);
        }

        [Test]
        public void CanParsePtsChunksThenImportThenLoadFromStore()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey("test")
                ;
            var ptsChunks = Data.Points.Import.Pts.Chunks(filename, config);
            var pointset = PointCloud.Chunks(ptsChunks, config);
            Assert.IsTrue(pointset.Id == "test");
            var pointset2 = config.Storage.GetPointSet("test", CancellationToken.None);
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 3);
        }

        #endregion

        #region E57

        [Test]
        public void CanParseE57FileInfo()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var info = PointCloud.ParseFileInfo(filename, ImportConfig.Default);

            Assert.IsTrue(info.PointCount == 3);
            Assert.IsTrue(info.Bounds == new Box3d(new V3d(1), new V3d(9)));
        }

        [Test]
        public void CanParseE57File()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var ps = PointCloud.Parse(filename, ImportConfig.Default)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(ps.Length == 3);
            Assert.IsTrue(ps[0].ApproxEqual(new V3d(1, 2, 9), 1e-10));
        }

        [Test]
        public void CanImportE57File()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey("test")
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset != null);
            Assert.IsTrue(pointset.PointCount == 3);
        }

        [Test]
        public void CanImportE57File_MinDist()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey("test")
                .WithMinDist(10)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset.PointCount < 3);
        }

        [Test]
        public void CanImportE57FileAndLoadFromStore()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey("test")
                ;
            var pointset = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test", CancellationToken.None);
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 3);
        }

        #endregion
    }
}
