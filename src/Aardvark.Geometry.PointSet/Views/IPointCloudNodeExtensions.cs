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
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class IPointCloudNodeExtensions
    {
        #region Has*

        private static bool Has(IPointCloudNode n, string attributeName)
        {
            switch (n.FilterState)
            {
                case FilterState.FullyOutside:
                    return false;
                case FilterState.FullyInside:
                case FilterState.Partial:
                    return n.TryGetPropertyKey(attributeName, out string _);
                default:
                    throw new InvalidOperationException($"Unknown FilterState {n.FilterState}.");
            }
        }

        /// <summary> </summary>
        public static bool HasPositions(this IPointCloudNode self) => Has(self, PointCloudAttribute.Positions);

        /// <summary></summary>
        public static bool HasColors(this IPointCloudNode self) => Has(self, PointCloudAttribute.Colors);

        /// <summary></summary>
        public static bool HasNormals(this IPointCloudNode self) => Has(self, PointCloudAttribute.Normals);

        /// <summary></summary>
        public static bool HasIntensities(this IPointCloudNode self) => Has(self, PointCloudAttribute.Intensities);

        /// <summary></summary>
        public static bool HasKdTree(this IPointCloudNode self) => Has(self, PointCloudAttribute.KdTree);
        
        /// <summary></summary>
        public static bool HasClassifications(this IPointCloudNode self) => Has(self, PointCloudAttribute.Classifications);
        
        #endregion

        #region Get*

        /// <summary>
        /// Point positions relative to cell's center, or null if no positions.
        /// </summary>
        public static PersistentRef<V3f[]> GetPositions(this IPointCloudNode self)
            => self.TryGetPropertyValue(PointCloudAttribute.Positions, out object value) ? (PersistentRef<V3f[]>)value : null;

        /// <summary>
        /// Point positions (absolute), or null if no positions.
        /// </summary>
        public static V3d[] GetPositionsAbsolute(this IPointCloudNode self)
        {
            var c = self.Center;
            var ps = GetPositions(self);
            if (ps == null) return null;
            return ps.Value.Map(p => new V3d(p.X + c.X, p.Y + c.Y, p.Z + c.Z));
        }

        /// <summary>
        /// </summary>
        public static PersistentRef<PointRkdTreeD<V3f[], V3f>> GetKdTree(this IPointCloudNode self)
            => self.TryGetPropertyValue(PointCloudAttribute.KdTree, out object value) ? (PersistentRef<PointRkdTreeD<V3f[], V3f>>)value : null;

        /// <summary>
        /// Point colors, or null if no points.
        /// </summary>
        public static PersistentRef<C4b[]> GetColors(this IPointCloudNode self)
            => self.TryGetPropertyValue(PointCloudAttribute.Colors, out object value) ? (PersistentRef<C4b[]>)value : null;

        /// <summary>
        /// </summary>
        public static PersistentRef<V3f[]> GetNormals(this IPointCloudNode self)
            => self.TryGetPropertyValue(PointCloudAttribute.Normals, out object value) ? (PersistentRef<V3f[]>)value : null;

        /// <summary>
        /// </summary>
        public static PersistentRef<int[]> GetIntensities(this IPointCloudNode self)
            => self.TryGetPropertyValue(PointCloudAttribute.Intensities, out object value) ? (PersistentRef<int[]>)value : null;

        /// <summary>
        /// </summary>
        public static PersistentRef<byte[]> GetClassifications(this IPointCloudNode self)
            => self.TryGetPropertyValue(PointCloudAttribute.Classifications, out object value) ? (PersistentRef<byte[]>)value : null;
        
        #endregion

        #region Storage

        /// <summary></summary>
        public static void Add(this Storage storage, string key, IPointCloudNode data, CancellationToken ct = default)
        {
            storage.f_add(key, data, () =>
            {
                var json = data.ToJson().ToString();
                var buffer = Encoding.UTF8.GetBytes(json);
                return buffer;
            }, ct);
        }

        /// <summary></summary>
        public static IPointCloudNode GetPointCloudNode(this Storage storage, string key, IStoreResolver resolver, CancellationToken ct = default)
        {
            var data = (IPointCloudNode)storage.f_tryGetFromCache(key, ct);
            if (data != null) return data;

            var buffer = storage.f_get(key, ct);
            if (buffer == null) return null;
            var json = JObject.Parse(Encoding.UTF8.GetString(buffer));

            var nodeType = (string)json["NodeType"];
            switch (nodeType)
            {
                case LinkedNode.Type:
                    data = LinkedNode.Parse(json, storage, resolver);
                    break;
                case PointCloudNode.Type:
                    data = PointCloudNode.Parse(json, storage, resolver);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown node type '{nodeType}'.");
            }

            storage.f_add(key, data, null, ct);
            return data;
        }

        #endregion

        /// <summary></summary>
        public static bool IsLeaf(this IPointCloudNode self) => self.SubNodes == null;

        /// <summary></summary>
        public static bool IsNotLeaf(this IPointCloudNode self) => self.SubNodes != null;

        /// <summary>
        /// Counts ALL nodes of this tree by traversing over all persistent refs.
        /// </summary>
        public static long CountNodes(this IPointCloudNode self)
        {
            if (self == null) return 0;

            var subnodes = self.SubNodes;
            if (subnodes == null) return 1;
            
            var count = 1L;
            for (var i = 0; i < 8; i++)
            {
                var n = subnodes[i];
                if (n == null) continue;
                count += n.Value.CountNodes();
            }
            return count;
        }

        #region Intersections, inside/outside, ...

        /// <summary>
        /// Index of subnode for given point.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetSubIndex(this IPointCloudNode self, in V3d p)
        {
            var i = 0;
            if (p.X > self.Center.X) i = 1;
            if (p.Y > self.Center.Y) i += 2;
            if (p.Z > self.Center.Z) i += 4;
            return i;
        }

        /// <summary>
        /// Returns true if this node intersects the positive halfspace defined by given plane.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IntersectsPositiveHalfSpace(this IPointCloudNode self, in Plane3d plane)
        {
            var corners = self.BoundingBoxExact.ComputeCorners();
            for (var i = 0; i < 8; i++)
            {
                if (plane.Height(corners[i]) > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if this node intersects the negative halfspace defined by given plane.
        /// </summary>
        public static bool IntersectsNegativeHalfSpace(this IPointCloudNode self, in Plane3d plane)
        {
            var corners = self.BoundingBoxExact.ComputeCorners();
            for (var i = 0; i < 8; i++)
            {
                if (plane.Height(corners[i]) < 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if this node is fully inside the positive halfspace defined by given plane.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsidePositiveHalfSpace(this IPointCloudNode self, in Plane3d plane)
        {
            self.BoundingBoxExact.GetMinMaxInDirection(plane.Normal, out V3d min, out V3d max);
            return plane.Height(min) > 0;
        }

        /// <summary>
        /// Returns true if this node is fully inside the negative halfspace defined by given plane.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsideNegativeHalfSpace(this IPointCloudNode self, in Plane3d plane)
        {
            self.BoundingBoxExact.GetMinMaxInDirection(-plane.Normal, out V3d min, out V3d max);
            return plane.Height(min) < 0;
        }

        #endregion

        #region ForEach (optionally traversing out-of-core nodes) 

        /// <summary>
        /// Calls action for each node in this tree.
        /// </summary>
        public static void ForEachNode(this IPointCloudNode self, bool outOfCore, Action<IPointCloudNode> action)
        {
            action(self);

            if (self.SubNodes == null) return;

            if (outOfCore)
            {
                for (var i = 0; i < 8; i++)
                {
                    self.SubNodes[i]?.Value.ForEachNode(outOfCore, action);
                }
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = self.SubNodes[i];
                    if (n != null)
                    {
                        if (n.TryGetValue(out IPointCloudNode node)) node.ForEachNode(outOfCore, action);
                    }
                }
            }
        }

        #endregion
    }
}
