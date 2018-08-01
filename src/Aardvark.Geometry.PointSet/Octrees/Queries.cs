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
using Aardvark.Base;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class Queries
    {
        #region V3d

        /// <summary>
        /// Points within given distance of a point.
        /// </summary>
        public static PointsNearObject<V3d> QueryPointsNearPoint(
            this PointSet self, V3d query, double maxDistanceToPoint, int maxCount
            )
            => QueryPointsNearPoint(self.Root.Value, query, maxDistanceToPoint, maxCount);

        /// <summary>
        /// Points within given distance of a point.
        /// </summary>
        public static PointsNearObject<V3d> QueryPointsNearPoint(
            this PointSetNode node, V3d query, double maxDistanceToPoint, int maxCount
            )
        {
            if (node == null) return PointsNearObject<V3d>.Empty;

            // if query point is farther from bounding box than maxDistanceToPoint,
            // then there cannot be a result and we are done
            var eps = node.BoundingBox.Distance(query);
            if (eps > maxDistanceToPoint) return PointsNearObject<V3d>.Empty;

            if (node.IsLeaf)
            {
                #region paranoid
                if (node.PointCount <= 0) throw new InvalidOperationException();
                #endregion

                var center = node.Center;

                var ia = node.KdTree.Value.GetClosest((V3f)(query - center), (float)maxDistanceToPoint, maxCount);
                if (ia.Count > 0)
                {
                    var ps = new V3d[ia.Count];
                    var cs = node.HasColors ? new C4b[ia.Count] : null;
                    var ns = node.HasNormals ? new V3f[ia.Count] : null;
                    var js = node.HasIntensities ? new int[ia.Count] : null;
                    var ds = new double[ia.Count];
                    for (var i = 0; i < ia.Count; i++)
                    {
                        var index = (int)ia[i].Index;
                        ps[i] = center + (V3d)node.Positions.Value[index];
                        if (node.HasColors) cs[i] = node.Colors.Value[index];
                        if (node.HasNormals) ns[i] = node.Normals.Value[index];
                        if (node.HasIntensities) js[i] = node.Intensities.Value[index];
                        ds[i] = ia[i].Dist;
                    }
                    var chunk = new PointsNearObject<V3d>(query, maxDistanceToPoint, ps, cs, ns, js, ds);
                    return chunk;
                }
                else
                {
                    return PointsNearObject<V3d>.Empty;
                }
            }
            else
            {
                // first traverse octant containing query point
                var index = node.GetSubIndex(query);
                var n = node.Subnodes[index];
                var result = n != null ? n.Value.QueryPointsNearPoint(query, maxDistanceToPoint, maxCount) : PointsNearObject<V3d>.Empty;
                if (!result.IsEmpty && result.MaxDistance < maxDistanceToPoint) maxDistanceToPoint = result.MaxDistance;

                // now traverse other octants
                for (var i = 0; i < 8; i++)
                {
                    if (i == index) continue;
                    n = node.Subnodes[i];
                    if (n == null) continue;
                    var x = n.Value.QueryPointsNearPoint(query, maxDistanceToPoint, maxCount);
                    result = result.Merge(x, maxCount);
                    if (!result.IsEmpty && result.MaxDistance < maxDistanceToPoint) maxDistanceToPoint = result.MaxDistance;
                }

                return result;
            }
        }

        #endregion

        #region Ray3d, Line3d

        /// <summary>
        /// Points within given distance of a ray (at most 1000).
        /// </summary>
        public static IEnumerable<PointsNearObject<Line3d>> QueryPointsNearRay(
            this PointSet self, Ray3d ray, double maxDistanceToRay
            )
        {
            ray.Direction = ray.Direction.Normalized;
            var data = self.Root.Value;
            var bbox = data.BoundingBox;

            var line = Clip(bbox, ray);
            if (!line.HasValue) return Enumerable.Empty<PointsNearObject<Line3d>>();

            return self.QueryPointsNearLineSegment(line.Value, maxDistanceToRay);
        }

        /// <summary>
        /// Points within given distance of a line segment (at most 1000).
        /// </summary>
        public static IEnumerable<PointsNearObject<Line3d>> QueryPointsNearLineSegment(
            this PointSet self, Line3d lineSegment, double maxDistanceToRay
            )
            => QueryPointsNearLineSegment(self.Root.Value, lineSegment, maxDistanceToRay);

        /// <summary>
        /// Points within given distance of a line segment (at most 1000).
        /// </summary>
        public static IEnumerable<PointsNearObject<Line3d>> QueryPointsNearLineSegment(
            this PointSetNode node, Line3d lineSegment, double maxDistanceToRay
            )
        {
            if (!node.BoundingBox.Intersects(lineSegment))
            {
                yield break;
            }
            else if (node.PointCount > 0)
            {
                var center = node.Center;
                var ia = node.KdTree.Value.GetClosestToLine((V3f)(lineSegment.P0 - center), (V3f)(lineSegment.P1 - center), (float)maxDistanceToRay, 1000);
                if (ia.Count > 0)
                {
                    var ps = new V3d[ia.Count];
                    var cs = node.HasColors ? new C4b[ia.Count] : null;
                    var ns = node.HasNormals ? new V3f[ia.Count] : null;
                    var js = node.HasIntensities ? new int[ia.Count] : null;
                    var ds = new double[ia.Count];
                    for (var i = 0; i < ia.Count; i++)
                    {
                        var index = (int)ia[i].Index;
                        ps[i] = center + (V3d)node.Positions.Value[index];
                        if (node.HasColors) cs[i] = node.Colors.Value[index];
                        if (node.HasNormals) ns[i] = node.Normals.Value[index];
                        ds[i] = ia[i].Dist;
                    }
                    var chunk = new PointsNearObject<Line3d>(lineSegment, maxDistanceToRay, ps, cs, ns, js, ds);
                    yield return chunk;
                }
            }
            else if (node.Subnodes != null)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = node.Subnodes[i];
                    if (n == null) continue;
                    foreach (var x in QueryPointsNearLineSegment(n.Value, lineSegment, maxDistanceToRay)) yield return x;
                }
            }
        }
        
        /// <summary>
        /// Clips given ray on box, or returns null if ray does not intersect box.
        /// </summary>
        private static Line3d? Clip(Box3d box, Ray3d ray0)
        {
            ray0.Direction = ray0.Direction.Normalized;

            if (!box.Intersects(ray0, out double t0)) return null;
            var p0 = ray0.GetPointOnRay(t0);

            var ray1 = new Ray3d(ray0.GetPointOnRay(t0 + box.Size.Length), -ray0.Direction);
            if (!box.Intersects(ray1, out double t1)) throw new InvalidOperationException();
            var p1 = ray1.GetPointOnRay(t1);

            return new Line3d(p0, p1);
        }

        #endregion

        #region Plane3d

        /// <summary>
        /// All points within maxDistance of given plane.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPlane(
            this PointSet self, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNearPlane(self.Root.Value, plane, maxDistance, minCellExponent);

        /// <summary>
        /// All points within maxDistance of given plane.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPlane(
            this PointSetNode node, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPoints(node,
                n => plane.Contains(maxDistance, node.BoundingBox),
                n => !node.BoundingBox.Intersects(plane, maxDistance),
                p => Math.Abs(plane.Height(p)) <= maxDistance,
                minCellExponent
                );

        /// <summary>
        /// All points within maxDistance of ANY of the given planes.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPlanes(
            this PointSet self, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNearPlanes(self.Root.Value, planes, maxDistance, minCellExponent);

        /// <summary>
        /// All points within maxDistance of ANY of the given planes.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPlanes(
            this PointSetNode node, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPoints(node,
                n => planes.Any(plane => plane.Contains(maxDistance, node.BoundingBox)),
                n => !planes.Any(plane => node.BoundingBox.Intersects(plane, maxDistance)),
                p => planes.Any(plane => Math.Abs(plane.Height(p)) <= maxDistance),
                minCellExponent
                );

        /// <summary>
        /// All points NOT within maxDistance of given plane.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPlane(
            this PointSet self, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNotNearPlane(self.Root.Value, plane, maxDistance, minCellExponent);

        /// <summary>
        /// All points NOT within maxDistance of given plane.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPlane(
            this PointSetNode node, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPoints(node,
                n => !node.BoundingBox.Intersects(plane, maxDistance),
                n => plane.Contains(maxDistance, node.BoundingBox),
                p => Math.Abs(plane.Height(p)) > maxDistance,
                minCellExponent
                );

        /// <summary>
        /// All points NOT within maxDistance of ALL the given planes.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPlanes(
            this PointSet self, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNotNearPlanes(self.Root.Value, planes, maxDistance, minCellExponent);

        /// <summary>
        /// All points NOT within maxDistance of ALL the given planes.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPlanes(
            this PointSetNode node, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPoints(node,
                n => !planes.Any(plane => node.BoundingBox.Intersects(plane, maxDistance)),
                n => planes.Any(plane => plane.Contains(maxDistance, node.BoundingBox)),
                p => !planes.Any(plane => Math.Abs(plane.Height(p)) <= maxDistance),
                minCellExponent
                );

        #endregion

        #region Polygon3d

        /// <summary>
        /// All points within maxDistance of given polygon.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPolygon(
            this PointSet self, Polygon3d polygon, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNearPolygon(self.Root.Value, polygon, maxDistance, minCellExponent);

        /// <summary>
        /// All points within maxDistance of given polygon.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPolygon(
            this PointSetNode node, Polygon3d polygon, double maxDistance, int minCellExponent = int.MinValue
            )
        {
            var bounds = polygon.BoundingBox3d(maxDistance);
            var plane = polygon.GetPlane3d();
            var w2p = plane.GetWorldToPlane();
            var poly2d = new Polygon2d(polygon.GetPointArray().Map(p => w2p.TransformPos(p).XY));
            return QueryPoints(node,
                n => false,
                n => !n.BoundingBox.Intersects(bounds),
                p => polygon.Contains(plane, w2p, poly2d, maxDistance, p, out double d),
                minCellExponent
                );
        }

        /// <summary>
        /// All points within maxDistance of ANY of the given polygons.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPolygons(
            this PointSet self, Polygon3d[] polygons, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNearPolygons(self.Root.Value, polygons, maxDistance, minCellExponent);

        /// <summary>
        /// All points within maxDistance of ANY of the given polygons.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPolygons(
            this PointSetNode node, Polygon3d[] polygons, double maxDistance, int minCellExponent = int.MinValue
            )
        {
            var bounds = polygons.Map(x => x.BoundingBox3d(maxDistance));
            var planes = polygons.Map(x => x.GetPlane3d());
            var w2p = planes.Map(x => x.GetWorldToPlane());
            var poly2d = polygons.Map((x, i) => new Polygon2d(x.GetPointArray().Map(p => w2p[i].TransformPos(p).XY)));
            return QueryPoints(node,
                n => false,
                n => !bounds.Any(b => n.BoundingBox.Intersects(b)),
                p => planes.Any((plane, i) => polygons[i].Contains(plane, w2p[i], poly2d[i], maxDistance, p, out double d)),
                minCellExponent
                );
        }

        /// <summary>
        /// All points NOT within maxDistance of given polygon.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPolygon(
            this PointSet self, Polygon3d polygon, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNotNearPolygon(self.Root.Value, polygon, maxDistance, minCellExponent);

        /// <summary>
        /// All points NOT within maxDistance of given polygon.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPolygon(
            this PointSetNode node, Polygon3d polygon, double maxDistance, int minCellExponent = int.MinValue
            )
        {
            var bounds = polygon.BoundingBox3d(maxDistance);
            var plane = polygon.GetPlane3d();
            var w2p = plane.GetWorldToPlane();
            var poly2d = new Polygon2d(polygon.GetPointArray().Map(p => w2p.TransformPos(p).XY));
            return QueryPoints(node,
                n => !n.BoundingBox.Intersects(bounds),
                n => false,
                p => !polygon.Contains(plane, w2p, poly2d, maxDistance, p, out double d),
                minCellExponent
                );
        }

        /// <summary>
        /// All points NOT within maxDistance of ALL the given polygons.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPolygons(
            this PointSet self, Polygon3d[] polygons, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNotNearPolygons(self.Root.Value, polygons, maxDistance, minCellExponent);

        /// <summary>
        /// All points NOT within maxDistance of ALL the given polygons.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPolygons(
            this PointSetNode node, Polygon3d[] polygons, double maxDistance, int minCellExponent = int.MinValue
            )
        {
            var bounds = polygons.Map(x => x.BoundingBox3d(maxDistance));
            var planes = polygons.Map(x => x.GetPlane3d());
            var w2p = planes.Map(x => x.GetWorldToPlane());
            var poly2d = polygons.Map((x, i) => new Polygon2d(x.GetPointArray().Map(p => w2p[i].TransformPos(p).XY)));
            return QueryPoints(node,
                n => !bounds.Any(b => n.BoundingBox.Intersects(b)),
                n => false,
                p => !planes.Any((plane, i) => polygons[i].Contains(plane, w2p[i], poly2d[i], maxDistance, p, out double d)),
                minCellExponent
                );
        }

        #endregion

        #region Hull3d (convex hull)

        /// <summary>
        /// All points inside convex hull (including boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInsideConvexHull(
            this PointSet self, Hull3d convexHull, int minCellExponent = int.MinValue
            )
            => QueryPointsInsideConvexHull(self.Root.Value, convexHull, minCellExponent);

        /// <summary>
        /// All points inside convex hull (including boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInsideConvexHull(
            this PointSetNode self, Hull3d convexHull, int minCellExponent = int.MinValue
            )
        {
            foreach (var x in self.ForEachNodeIntersecting(convexHull, true, minCellExponent))
            {
                if (x.IsFullyInside)
                {
                    foreach (var y in x.Cell.ForEachNode())
                    {
                        if (y.PointCount == 0) continue;
                        var chunk = new Chunk(y.PositionsAbsolute, y.Colors?.Value, y.Normals?.Value);
                        yield return chunk;
                    }
                }
                else
                {
                    var n = x.Cell;
                    if (n.PointCount == 0) continue;
                    var ps = new List<V3d>();
                    var cs = n.HasColors ? new List<C4b>() : null;
                    var ns = n.HasNormals ? new List<V3f>() : null;
                    var positionsAbsolute = n.PositionsAbsolute;
                    for (var i = 0; i < positionsAbsolute.Length; i++)
                    {
                        if (convexHull.Contains(positionsAbsolute[i]))
                        {
                            ps.Add(positionsAbsolute[i]);
                            if (n.HasColors) cs.Add(n.Colors.Value[i]);
                            if (n.HasNormals) ns.Add(n.Normals.Value[i]);
                        }
                    }

                    var chunk = new Chunk(ps, cs, ns);
                    yield return chunk;
                }
            }
        }

        /// <summary>
        /// All points outside convex hull (excluding boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsOutsideConvexHull(
            this PointSet self, Hull3d convexHull, int minCellExponent = int.MinValue
            )
            => QueryPointsOutsideConvexHull(self.Root.Value, convexHull, minCellExponent);

        /// <summary>
        /// All points outside convex hull (excluding boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsOutsideConvexHull(
            this PointSetNode self, Hull3d convexHull, int minCellExponent = int.MinValue
            )
            => QueryPointsInsideConvexHull(self, convexHull.Reversed(), minCellExponent);
        
        #region Count

        /// <summary>
        /// Counts points inside convex hull (approximately).
        /// Result is always equal or greater than exact number.
        /// </summary>
        internal static long CountPointsInsideConvexHull(
            this PointSet self, Hull3d convexHull, int minCellExponent = int.MinValue
            )
            => CountPointsInsideConvexHull(self.Root.Value, convexHull, minCellExponent);

        /// <summary>
        /// Counts points inside convex hull (approximately).
        /// Result is always equal or greater than exact number.
        /// </summary>
        internal static long CountPointsInsideConvexHull(
            this PointSetNode self, Hull3d convexHull, int minCellExponent = int.MinValue
            )
            => self.ForEachNodeIntersecting(convexHull, false, minCellExponent).Sum(x => x.Cell.LodPointCount);

        /// <summary>
        /// Counts points outside convex hull (approximately).
        /// Result is always equal or greater than exact number.
        /// </summary>
        internal static long CountPointsOutsideConvexHull(
            this PointSet self, Hull3d convexHull, int minCellExponent = int.MinValue
            )
            => CountPointsOutsideConvexHull(self.Root.Value, convexHull, minCellExponent);

        /// <summary>
        /// Counts points outside convex hull (approximately).
        /// Result is always equal or greater than exact number.
        /// </summary>
        internal static long CountPointsOutsideConvexHull(
            this PointSetNode self, Hull3d convexHull, int minCellExponent = int.MinValue
            )
            => CountPointsOutsideConvexHull(self, convexHull.Reversed(), minCellExponent);

        #endregion

        #endregion

        #region Box3d

        /// <summary>
        /// All points inside axis-aligned box (including boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInsideBox(
            this PointSet self, Box3d boundingBox, int minCellExponent = int.MinValue
            )
            => QueryPointsInsideConvexHull(self, Hull3d.Create(boundingBox), minCellExponent);

        /// <summary>
        /// All points inside axis-aligned box (including boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInsideBox(
            this PointSetNode self, Box3d boundingBox, int minCellExponent = int.MinValue
            )
            => QueryPointsInsideConvexHull(self, Hull3d.Create(boundingBox), minCellExponent);

        /// <summary>
        /// All points outside axis-aligned box (excluding boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsOutsideBox(
            this PointSet self, Box3d boundingBox, int minCellExponent = int.MinValue
            )
            => QueryPointsOutsideConvexHull(self, Hull3d.Create(boundingBox), minCellExponent);

        /// <summary>
        /// All points outside axis-aligned box (excluding boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsOutsideBox(
            this PointSetNode self, Box3d boundingBox, int minCellExponent = int.MinValue
            )
            => QueryPointsOutsideConvexHull(self, Hull3d.Create(boundingBox), minCellExponent);
        
        #region Count

        /// <summary>
        /// Counts points inside axis-aligned box (approximately).
        /// Result is always equal or greater than exact number.
        /// </summary>
        internal static long CountPointsInsideBox(
            this PointSet self, Box3d boundingBox, int minCellExponent = int.MinValue
            )
            => CountPointsInsideConvexHull(self, Hull3d.Create(boundingBox), minCellExponent);

        /// <summary>
        /// Counts points inside axis-aligned box (approximately).
        /// Result is always equal or greater than exact number.
        /// </summary>
        internal static long CountPointsInsideBox(
            this PointSetNode self, Box3d boundingBox, int minCellExponent = int.MinValue
            )
            => CountPointsInsideConvexHull(self, Hull3d.Create(boundingBox), minCellExponent);

        /// <summary>
        /// Counts points outside axis-aligned box (approximately).
        /// Result is always equal or greater than exact number.
        /// </summary>
        internal static long CountPointsOutsideBox(
            this PointSet self, Box3d boundingBox, int minCellExponent = int.MinValue
            )
            => CountPointsOutsideConvexHull(self, Hull3d.Create(boundingBox), minCellExponent);

        /// <summary>
        /// Counts points outside axis-aligned box (approximately).
        /// Result is always equal or greater than exact number.
        /// </summary>
        internal static long CountPointsOutsideBox(
            this PointSetNode self, Box3d boundingBox, int minCellExponent = int.MinValue
            )
            => CountPointsOutsideConvexHull(self, Hull3d.Create(boundingBox), minCellExponent);

        #endregion

        #endregion

        #region View frustum

        /// <summary>
        /// Returns points inside view frustum (defined by viewProjection and canonicalViewVolume).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInViewFrustum(
            this PointSet self, M44d viewProjection, Box3d canonicalViewVolume
            )
        {
            var t = viewProjection.Inverse;
            var cs = canonicalViewVolume.ComputeCorners().Map(t.TransformPosProj);
            var hull = new Hull3d(new[]
            {
                new Plane3d(cs[0], cs[2], cs[1]), // near
                new Plane3d(cs[5], cs[7], cs[4]), // far
                new Plane3d(cs[0], cs[1], cs[4]), // bottom
                new Plane3d(cs[1], cs[3], cs[5]), // left
                new Plane3d(cs[4], cs[6], cs[0]), // right
                new Plane3d(cs[3], cs[2], cs[7]), // top
            });

            return QueryPointsInsideConvexHull(self, hull);
        }

        #endregion

        #region Octree levels

        /// <summary>
        /// Max tree depth.
        /// </summary>
        public static int CountOctreeLevels(this PointSet self)
            => CountOctreeLevels(self.Root.Value);

        /// <summary>
        /// Max tree depth.
        /// </summary>
        public static int CountOctreeLevels(this PointSetNode root)
        {
            if (root == null) return 0;
            if (root.Subnodes == null) return 1;
            return root.Subnodes.Select(n => CountOctreeLevels(n?.Value)).Max() + 1;
        }



        /// <summary>
        /// Finds deepest octree level which still contains less than given number of points. 
        /// </summary>
        public static int GetMaxOctreeLevelWithLessThanGivenPointCount(
            this PointSet self, long maxPointCount
            )
            => GetMaxOctreeLevelWithLessThanGivenPointCount(self.Root.Value, maxPointCount);

        /// <summary>
        /// Finds deepest octree level which still contains less than given number of points. 
        /// </summary>
        public static int GetMaxOctreeLevelWithLessThanGivenPointCount(
            this PointSetNode node, long maxPointCount
            )
        {
            var imax = node.CountOctreeLevels();
            for (var i = 0; i < imax; i++)
            {
                var count = node.CountPointsInOctreeLevel(i);
                if (count >= maxPointCount) return i - 1;
            }

            return imax - 1;
        }



        /// <summary>
        /// Finds deepest octree level which still contains less than given number of points within given bounds. 
        /// </summary>
        public static int GetMaxOctreeLevelWithLessThanGivenPointCount(
            this PointSet self, long maxPointCount, Box3d bounds
            )
            => GetMaxOctreeLevelWithLessThanGivenPointCount(self.Root.Value, maxPointCount, bounds);

        /// <summary>
        /// Finds deepest octree level which still contains less than given number of points within given bounds. 
        /// </summary>
        public static int GetMaxOctreeLevelWithLessThanGivenPointCount(
            this PointSetNode node, long maxPointCount, Box3d bounds
            )
        {
            var imax = node.CountOctreeLevels();
            for (var i = 0; i < imax; i++)
            {
                var count = node.CountPointsInOctreeLevel(i, bounds);
                if (count >= maxPointCount) return i - 1;
            }

            return imax - 1;
        }



        /// <summary>
        /// Gets total number of points in all cells at given octree level.
        /// </summary>
        public static long CountPointsInOctreeLevel(
            this PointSet self, int level
            )
            => CountPointsInOctreeLevel(self.Root.Value, level);

        /// <summary>
        /// Gets total number of lod-points in all cells at given octree level.
        /// </summary>
        public static long CountPointsInOctreeLevel(
            this PointSetNode node, int level
            )
        {
            if (level < 0) return 0;

            if (level == 0 || node.IsLeaf)
            {
                return node.LodPointCount;
            }
            else
            {
                var nextLevel = level - 1;
                var sum = 0L;
                for (var i = 0; i < 8; i++)
                {
                    var n = node.Subnodes[i];
                    if (n == null) continue;
                    sum += CountPointsInOctreeLevel(n.Value, nextLevel);
                }
                return sum;
            }
        }



        /// <summary>
        /// Gets approximate number of points at given octree level within given bounds.
        /// For cells that only partially overlap the specified bounds all points are counted anyway.
        /// For performance reasons, in order to avoid per-point bounds checks.
        /// </summary>
        public static long CountPointsInOctreeLevel(
            this PointSet self, int level, Box3d bounds
            )
            => CountPointsInOctreeLevel(self.Root.Value, level, bounds);

        /// <summary>
        /// Gets approximate number of points at given octree level within given bounds.
        /// For cells that only partially overlap the specified bounds all points are counted anyway.
        /// For performance reasons, in order to avoid per-point bounds checks.
        /// </summary>
        public static long CountPointsInOctreeLevel(
            this PointSetNode node, int level, Box3d bounds
            )
        {
            if (level < 0) return 0;
            if (!node.BoundingBox.Intersects(bounds)) return 0;

            if (level == 0 || node.IsLeaf)
            {
                return node.LodPointCount;
            }
            else
            {
                var nextLevel = level - 1;
                var sum = 0L;
                for (var i = 0; i < 8; i++)
                {
                    var n = node.Subnodes[i];
                    if (n == null) continue;
                    sum += CountPointsInOctreeLevel(n.Value, nextLevel, bounds);
                }
                return sum;
            }
        }



        /// <summary>
        /// Returns points in given octree level, where level 0 is the root node.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInOctreeLevel(
            this PointSet self, int level
            )
            => QueryPointsInOctreeLevel(self.Root.Value, level);

        /// <summary>
        /// Returns lod points for given octree depth/front, where level 0 is the root node.
        /// Front will include leafs higher up than given level.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInOctreeLevel(
            this PointSetNode node, int level
            )
        {
            if (level < 0) yield break;

            if (level == 0 || node.IsLeaf)
            {
                var ps = node.LodPositionsAbsolute;
                var cs = node?.LodColors?.Value;
                var ns = node?.LodNormals?.Value;
                var chunk = new Chunk(ps, cs, ns);
                yield return chunk;
            }
            else
            {
                if (node.Subnodes == null) yield break;

                for (var i = 0; i < 8; i++)
                {
                    var n = node.Subnodes[i];
                    if (n == null) continue;
                    foreach (var x in QueryPointsInOctreeLevel(n.Value, level - 1)) yield return x;
                }
            }
        }



        /// <summary>
        /// Returns lod points for given octree depth/front of cells intersecting given bounds, where level 0 is the root node.
        /// Front will include leafs higher up than given level.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInOctreeLevel(
            this PointSet self, int level, Box3d bounds
            )
            => QueryPointsInOctreeLevel(self.Root.Value, level, bounds);

        /// <summary>
        /// Returns lod points for given octree depth/front of cells intersecting given bounds, where level 0 is the root node.
        /// Front will include leafs higher up than given level.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInOctreeLevel(
            this PointSetNode node, int level, Box3d bounds
            )
        {
            if (level < 0) yield break;
            if (!node.BoundingBox.Intersects(bounds)) yield break;

            if (level == 0 || node.IsLeaf)
            {
                var ps = node.LodPositionsAbsolute;
                var cs = node?.LodColors?.Value;
                var ns = node?.LodNormals?.Value;
                var chunk = new Chunk(ps, cs, ns);
                yield return chunk;
            }
            else
            {
                if (node.Subnodes == null) yield break;

                for (var i = 0; i < 8; i++)
                {
                    var n = node.Subnodes[i];
                    if (n == null) continue;
                    foreach (var x in QueryPointsInOctreeLevel(n.Value, level - 1, bounds)) yield return x;
                }
            }
        }

        #endregion

        #region All points

        /// <summary>
        /// Returns all points in pointset.
        /// </summary>
        public static IEnumerable<Chunk> QueryAllPoints(this PointSet self) => QueryAllPoints(self.Root.Value);

        /// <summary>
        /// Returnd all points in tree.
        /// </summary>
        public static IEnumerable<Chunk> QueryAllPoints(this PointSetNode node)
            => node.QueryPoints(_ => true, _ => false, _ => true);

        #endregion

        /// <summary>
        /// </summary>
        public static IEnumerable<Chunk> QueryPoints(this PointSet node,
            Func<PointSetNode, bool> isNodeFullyInside,
            Func<PointSetNode, bool> isNodeFullyOutside,
            Func<V3d, bool> isPositionInside,
            int minCellExponent = int.MinValue
            )
            => QueryPoints(node.Root.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent);

        /// <summary>
        /// </summary>
        /// <param name="node"></param>
        /// <param name="isNodeFullyInside"></param>
        /// <param name="isNodeFullyOutside"></param>
        /// <param name="isPositionInside"></param>
        /// <param name="minCellExponent">Limit traversal depth to minCellExponent (inclusive).</param>
        /// <returns></returns>
        public static IEnumerable<Chunk> QueryPoints(this PointSetNode node,
            Func<PointSetNode, bool> isNodeFullyInside,
            Func<PointSetNode, bool> isNodeFullyOutside,
            Func<V3d, bool> isPositionInside,
            int minCellExponent = int.MinValue
            )
        {
            if (node.Cell.Exponent < minCellExponent) yield break;

            if (isNodeFullyOutside(node)) yield break;
            
            if (node.IsLeaf || node.Cell.Exponent == minCellExponent)
            {
                if (isNodeFullyInside(node))
                {
                    if (node.HasPositions)
                    {
                        yield return new Chunk(node.PositionsAbsolute, node.Colors?.Value, node.Normals?.Value);
                    }
                    else if (node.HasLodPositions)
                    {
                        yield return new Chunk(node.LodPositionsAbsolute, node.LodColors?.Value, node.LodNormals?.Value);
                    }
                    yield break;
                }
                
                var psRaw = node.HasPositions ? node.PositionsAbsolute : node.LodPositionsAbsolute;
                var csRaw = node.HasColors ? node.Colors?.Value : node.LodColors?.Value;
                var nsRaw = node.HasNormals ? node.Normals?.Value : node.LodNormals?.Value;
                var ps = new List<V3d>();
                var cs = csRaw != null ? new List<C4b>() : null;
                var ns = nsRaw != null ? new List<V3f>() : null;
                for (var i = 0; i < psRaw.Length; i++)
                {
                    var p = psRaw[i];
                    if (isPositionInside(p))
                    {
                        ps.Add(p);
                        if (csRaw != null) cs.Add(csRaw[i]);
                        if (nsRaw != null) ns.Add(nsRaw[i]);
                    }
                }
                if (ps.Count > 0)
                {
                    yield return new Chunk(ps, cs, ns);
                }
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = node.Subnodes[i];
                    if (n == null) continue;
                    var xs = QueryPoints(n.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent);
                    foreach (var x in xs) yield return x;
                }
            }
        }
    }
}
