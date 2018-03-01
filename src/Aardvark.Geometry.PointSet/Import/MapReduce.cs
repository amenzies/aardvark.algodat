﻿/*
    Copyright (C) 2017. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Aardvark.Base;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static partial class ImportExtensions
    {
        /// <summary>
        /// Maps a sequence of point chunks to point sets, which are then reduced to one single point set.
        /// </summary>
        public static PointSet MapReduce(this IEnumerable<Chunk> chunks, ImportConfig config)
        {
            var totalChunkCount = 0;
            var totalPointCountInChunks = 0L;
            Action<double> progress = x => config.ProgressCallback(x * 0.5);
            
            #region MAP: create one PointSet for each chunk

            var pointsets = chunks
                .MapParallel((chunk, ct2) =>
                {
                    Interlocked.Add(ref totalPointCountInChunks, chunk.Count);
                    progress(1.0 - 1.0 / Interlocked.Increment(ref totalChunkCount));

                    var builder = InMemoryPointSet.Build(chunk, config.OctreeSplitLimit);
                    var root = builder.ToPointSetCell(config.Storage, ct: ct2);
                    var id = $"Aardvark.Geometry.PointSet.{Guid.NewGuid()}.json";
                    var pointSet = new PointSet(config.Storage, id, root.Id, config.OctreeSplitLimit);
                    
                    return pointSet;
                },
                config.MaxDegreeOfParallelism, null, config.CancellationToken
                )
                .ToList()
                ;
            ;

            if (config.Verbose)
            {
                Console.WriteLine($"[MapReduce] pointsets              : {pointsets.Count}");
                Console.WriteLine($"[MapReduce] totalPointCountInChunks: {totalPointCountInChunks}");
            }

            #endregion

            #region REDUCE: pairwise octree merge until a single (final) octree remains

            progress = x => config.ProgressCallback(0.5 + x * 0.5);
            var i = 0;

            var totalPointsToMerge = pointsets.Sum(x => x.PointCount);
            if (config.Verbose) Console.WriteLine($"[MapReduce] totalPointsToMerge: {totalPointsToMerge}");

            var totalPointSetsCount = pointsets.Count;
            if (totalPointSetsCount == 0) throw new Exception("woohoo");
            var final = pointsets.MapReduceParallel((first, second, ct2) =>
            {
                progress(Interlocked.Increment(ref i) / (double)totalPointSetsCount);
                var merged = first.Merge(second, ct2);
                config.Storage.Add(merged.Id, merged, ct2);
                if (config.Verbose) Console.WriteLine($"[MapReduce] merged "
                    + $"{first.Root.Value.Cell} + {second.Root.Value.Cell} -> {merged.Root.Value.Cell} "
                    + $"({first.Root.Value.PointCountTree} + {second.Root.Value.PointCountTree} -> {merged.Root.Value.PointCountTree})"
                    );

                if (merged.Root.Value.PointCountTree == 0) throw new InvalidOperationException();

                return merged;
            },
            config.MaxDegreeOfParallelism
            );
            if (config.Verbose)
            {
                Console.WriteLine($"[MapReduce] everything merged");
            }

            config.CancellationToken.ThrowIfCancellationRequested();

            #endregion

            config.Storage.Add(config.Key, final, config.CancellationToken);
            config.ProgressCallback(1.0);
            return final;
        }
    }
}
