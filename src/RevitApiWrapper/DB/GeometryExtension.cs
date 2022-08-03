#region Headers
/* ____________________________________________________________
*   DESCRIPTION: GeometryExtension
*   AUTHOR: Young
*   CREARETIME: 6/19/2022 8:35:55 PM 
*   CLRVERSION: 4.0.30319.42000
*  ____________________________________________________________
*/
#endregion

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace RevitApiWrapper.DB
{
    public static class GeometryExtension
    {
        #region Curve

        /// <summary>
        /// 
        /// </summary>
        /// <param name="curve"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static XYZ GetStartPoint(this Curve curve)
        {
            if (curve is null)
            {
                throw new ArgumentNullException(nameof(curve));
            }
            return curve.GetEndPoint(0);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="curve"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static XYZ GetEndPoint(this Curve curve)
        {
            if (curve is null)
            {
                throw new ArgumentNullException(nameof(curve));
            }
            return curve.GetEndPoint(1);
        }

        public static XYZ GetMiddlePoint(this Curve curve)
        {
            if (curve is null)
            {
                throw new ArgumentNullException(nameof(curve));
            }
            return curve.Evaluate(0.5, true);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static IList<XYZ> GetIntersectPoints(this Curve source, Curve target)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var result = source.Intersect(target, out var resultArray);
            if (result == SetComparisonResult.Overlap)
            {
                return resultArray.OfType<IntersectionResult>().Select(i => i.XYZPoint).ToArray();
            }
            return default;
        }

        public static IList<XYZ> GetIntersectPoints(this IList<Curve> curves)
        {
            if (curves is null)
            {
                throw new ArgumentNullException(nameof(curves));
            }
            var points = new List<XYZ>();
            for (int i = curves.Count - 1; i > 0; i--)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if (curves[i].Intersect(curves[j], out var resultArray) == SetComparisonResult.Overlap)
                    {
                        for (int k = 0; k < resultArray.Size; k++)
                        {
                            points.Add(resultArray.get_Item(k).XYZPoint);
                        }
                    }
                }
            }
            return points;
        }

        /// <summary>
        /// 将首尾闭合的曲线进行首尾相连排序
        /// </summary>
        /// <param name="curves"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="Exception"></exception>
        public static void SortCurvesContiguous(this IList<Curve> curves)
        {
            var sixteenth = 1d / 12d / 16d;
            if (curves is null)
            {
                throw new ArgumentNullException(nameof(curves));
            }
            var curveCount = curves.Count;
            for (int i = 0; i < curveCount; i++)
            {
                var curve = curves[i];
                var endPoint = curve.GetEndPoint();

                var found = i + 1 >= curveCount;
                for (int j = i + 1; j < curveCount; ++j)
                {
                    var point = curves[j].GetStartPoint();

                    if (point.DistanceTo(endPoint).IsGreaterThan(sixteenth))
                    {
                        if (i + 1 != j)
                        {
                            var tempCurve = curves[i + 1];
                            curves[i + 1] = curves[j];
                            curves[j] = tempCurve;
                        }
                        found = true;
                        break;
                    }
                    point = curves[j].GetEndPoint();
                    if (point.DistanceTo(endPoint).IsGreaterThan(sixteenth))
                    {
                        if (i + 1 == j)
                        {
                            curves[i + 1] = curves[j].CreateReversedCurve();
                        }
                        else
                        {
                            var tempCurve = curves[i + 1];
                            curves[i + 1] = curves[j].CreateReversedCurve();
                            curves[j] = tempCurve;
                        }
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    throw new Exception("SortCurvesContiguous:" + " non-contiguous input curves");
                }
            }
        }

        private static Curve CreateReversedCurve(this Curve curve)
        {
            if (curve is null)
            {
                throw new ArgumentNullException(nameof(curve));
            }
            if (!(curve is Line || curve is Arc))
            {
                throw new NotImplementedException($"CreateReversedCurve for type {curve.GetType().Name}");
            }
            switch (curve)
            {
                case Line line:
                    return Line.CreateBound(line.GetEndPoint(), line.GetStartPoint());
                case Arc arc:
                    return Arc.Create(arc.GetEndPoint(), arc.GetStartPoint(), arc.Evaluate(0.5, true));
            }
            throw new Exception("CreateReversedCurve - Unreachable");
        }
        #endregion
        /// <summary>
        /// 通过向量对封闭路径进行偏移(扩大)
        /// </summary>
        /// <param name="curveLoop"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        /// <remarks>
        /// 第二种方法：来源:https://blog.csdn.net/happy__888/article/details/315762 
        /// 此方法适用于平行直线结合形成的折线多边形的原位缩小与放大
        /// 通过缩放前点与缩放后的点及两点形成的向量(a,b)可组成四边相同的菱形形状
        /// 平行四边形面积：|a X b| = |a|*|b|*sin(θ) ,
        /// 已知偏移的长度，及两条平行线段的垂线距离L，可以在菱形边做垂线获得一个直角三角形
        /// 通过三角形定理，L/Lb = sin(Π - θ) 又 sin(Π - θ) = sin(θ)
        /// 所以可以得到最终公式: Lb = L/|a X b|/|a|/|b|
        /// 又最终点是起点(Ps) + 向量方向(a + b) * 向量长度(Lb)[因为之前求取向量将向量简化为单位向量，所以起点到中点的距离应该是菱形边会到未
        /// 单位化之前的值 及 normal(Lb) / Lb = normal(LTargetPoint) / LTargetPoint]
        /// 所以通过上述公式可以将最终点求出
        /// </remarks>
        public static CurveLoop OffsetPath(this CurveLoop curveLoop, double offset)
        {
            if (curveLoop is null)
            {
                throw new ArgumentNullException(nameof(curveLoop));
            }

            var vertices = new List<XYZ>();
            var newVertices = new List<XYZ>();
            //因为Revit中顶点都是逆时针排序，只需要取出点即可
            foreach (var curve in curveLoop)
            {
                vertices.Add(curve.GetEndPoint(0));
            }
            //每个点遍历获取前一个点与后一个点，获取两个向量，此处位置的**向量方向会与缩放形式有关**
            for (int i = 0; i < vertices.Count; i++)
            {
                int iPrevious;
                int iEnd;
                if (i == 0)
                {
                    iPrevious = vertices.Count - 1;
                    iEnd = i + 1;
                }
                else if (i == vertices.Count - 1)
                {
                    iPrevious = i - 1;
                    iEnd = 0;
                }
                else
                {
                    iPrevious = i - 1;
                    iEnd = i + 1;
                }
                var pPrevious = vertices[iPrevious];
                var point = vertices[i];
                var pEnd = vertices[iEnd];

                //normalize
                var v1 = (pPrevious - point).Normalize();
                var v2 = (pEnd - point).Normalize();
                var cross = v1.X * v2.Y - v1.Y * v2.X;//叉积 , v1 , v2单位向量模为1
                if (cross.IsAlmostEqualZero())
                {
                    continue;
                }
                double lb = offset / cross;
                var tPoint = point + lb * (v1 + v2);
                newVertices.Add(tPoint);
            }
            //output 
            var loop = new CurveLoop();
            for (int i = 0; i < newVertices.Count; i++)
            {
                if (i == newVertices.Count - 1)
                {
                    loop.Append(Line.CreateBound(newVertices[i], newVertices[0]));
                }
                else
                {
                    loop.Append(Line.CreateBound(newVertices[i], newVertices[i + 1]));
                }
            }
            return loop;
        }





        /// <summary>
        /// 两点形成直线在已知方向上的投影直线
        /// </summary>
        /// <param name="l">两点形成直线</param>
        /// <param name="Ori">已知方向向量</param>
        /// <returns></returns>
        public static Line ProjectionLineOnOri(this Line l, XYZ Ori)
        {
            XYZ p1 = l.GetEndPoint(0);
            XYZ p2 = l.GetEndPoint(1);
            Line OriLine = Line.CreateUnbound(p1, Ori);
            XYZ p1_ = OriLine.Project(p1).XYZPoint;
            XYZ p2_ = OriLine.Project(p2).XYZPoint;
            if (p1_.DistanceTo(p2) < 1e-6) { return null; }
            Line rL = Line.CreateBound(p1_, p2_);
            return rL;
        }


        /// <summary>
        /// 返回一个点在平面上的投影点坐标
        /// </summary>
        /// <param name="xyz">平面外任一点坐标</param>
        /// <param name="planeP">平面上的任一点坐标</param>
        /// <param name="planeNormal">平面的法向量</param>
        /// <returns></returns>
        public static XYZ GetPointOnPlane(this XYZ xyz, XYZ planeP, XYZ planeNormal)
        {
            double x = 0;
            double y = 0;
            double z = 0;

            x = (planeNormal.X * planeNormal.X * planeP.X + planeNormal.Y * planeNormal.Y * xyz.X - planeNormal.Y * xyz.Y * planeNormal.X + planeNormal.Y * planeP.Y * planeNormal.X + planeNormal.Z * planeNormal.Z * xyz.X - planeNormal.Z * xyz.Z * planeNormal.X + planeNormal.Z * planeP.Z * planeNormal.X) / (planeNormal.X * planeNormal.X + planeNormal.Y * planeNormal.Y + planeNormal.Z * planeNormal.Z);
            y = (planeNormal.Y * planeNormal.Z * planeP.Z + planeNormal.Z * planeNormal.Z * xyz.Y - planeNormal.Y * planeNormal.Z * xyz.Z + planeNormal.Y * planeNormal.X * planeP.X + planeNormal.X * planeNormal.X * xyz.Y - planeNormal.X * planeNormal.Y * xyz.X + planeNormal.Y * planeNormal.Y * planeP.Y) / (planeNormal.X * planeNormal.X + planeNormal.Y * planeNormal.Y + planeNormal.Z * planeNormal.Z);
            z = (planeNormal.X * planeP.X * planeNormal.Z + planeNormal.X * planeNormal.X * xyz.Z - planeNormal.X * xyz.X * planeNormal.Z + planeNormal.Y * planeP.Y * planeNormal.Z + planeNormal.Y * planeNormal.Y * xyz.Z - planeNormal.Y * xyz.Y * planeNormal.Z + planeNormal.Z * planeNormal.Z * planeP.Z) / (planeNormal.X * planeNormal.X + planeNormal.Y * planeNormal.Y + planeNormal.Z * planeNormal.Z);

            return new XYZ(x, y, z);
        }



        /// <summary>
        /// 返回直线在平面上的投影直线
        /// </summary>
        /// <param name="line">平面外的一条直线</param>
        /// <param name="planeP">平面上的任一点坐标</param>
        /// <param name="planeNormal">平面的法向量</param>
        /// <returns></returns>
        public static Line GetLineOnPlane(this Line line, XYZ planeP, XYZ planeNormal)
        {
            if (line.IsBound)
            {
                Line newl = Line.CreateBound(GetPointOnPlane(line.GetEndPoint(0), planeP, planeNormal), GetPointOnPlane(line.GetEndPoint(1), planeP, planeNormal));
                return newl;
            }
            else
            {
                XYZ newOri = (GetPointOnPlane(line.Origin + line.Direction * 100, planeP, planeNormal) - GetPointOnPlane(line.Origin, planeP, planeNormal)).Normalize();
                Line newl = Line.CreateUnbound(GetPointOnPlane(line.Origin, planeP, planeNormal), newOri);
                return newl;
            }
        }

        /// <summary>
        /// 返回两条直线交点方法
        /// </summary>
        /// <param name="line_1">第一条直线</param>
        /// <param name="line_2">第二条直线</param>
        /// <returns></returns>
        public static XYZ Line_IEresult(this Line line_1, Line line_2)
        {
            XYZ xyz = null;
            IntersectionResultArray result;
            SetComparisonResult scr = line_1.Intersect(line_2, out result);
            if (scr == SetComparisonResult.Overlap)//不重合
            {
                if (SetComparisonResult.Disjoint != scr)//相交
                {
                    xyz = result.get_Item(0).XYZPoint;
                }
            }
            return xyz;
        }


        /// <summary>
        /// 返回两个向量定向旋转的夹角(弧度值)，第二个向量为基准向量, rotateOri 为 ViewDirection 为顺时针，rotateOri为 ViewDirection.Negate() 为逆时针
        /// </summary>
        /// <param name="a">第一个向量</param>
        /// <param name="b">第二个向量（基准向量）</param>
        /// <param name="rotateOri">旋转方向，ViewDirection 为顺时针，ViewDirection.Negate() 为逆时针</param>
        /// <returns></returns>
        public static double SignedAngleBetween(this XYZ a, XYZ b, XYZ rotateOri)
        {
            double angle = a.AngleTo(b);

            int sign = Math.Sign(rotateOri.DotProduct(a.CrossProduct(b)));

            double signed_angle = angle * sign;

            if (signed_angle == 0)
            {
                if (a.IsAlmostEqualTo(b, 1e-6)) { signed_angle = 0; }
                else { signed_angle = Math.PI; }
            }

            else if (signed_angle < 0)
            {
                signed_angle = Math.PI * 2 + signed_angle;
            }

            return signed_angle;
        }





        /// <summary>
        /// 获得线与面的交点
        /// </summary>
        /// <param name="face"></param>
        /// <param name="line"></param>
        /// <returns></returns>
        public static XYZ CurveIntersectFace(this Face face, Line line)
        {

            XYZ returnXYZ = null;

            IntersectionResultArray ira = new IntersectionResultArray();

            SetComparisonResult scr = face.Intersect(line, out ira);

            if (scr != SetComparisonResult.Disjoint)
            {

                if (ira == null)
                {
                    return returnXYZ;
                }

                if (!ira.IsEmpty)
                {
                    returnXYZ = ira.get_Item(0).XYZPoint;
                }
            }

            return returnXYZ;
        }


        /// <summary>
        /// 得到一个点沿着一个直线轴旋转一定角度后的点坐标
        /// </summary>
        /// <param name="axis">直线轴：可以为射线</param>
        /// <param name="angle">旋转角度值</param>
        /// <param name="p">已知点坐标</param>
        /// <returns></returns>
        public static XYZ GetPointRotateAlongAxis(this Line axis, double angle, XYZ p)
        {
            XYZ rp = p;

            Line axis2 = axis;
            if (!axis.IsBound) { axis2 = Line.CreateBound(axis.Origin, axis.Origin + 1000 * axis.Direction); }

            XYZ v = (p - axis2.GetEndPoint(0)).Normalize();

            XYZ u = (axis2.GetEndPoint(1) - axis2.GetEndPoint(0)).Normalize();

            double a = AngleToRadian(angle);

            XYZ vv = Math.Cos(a) * v + (1 - Math.Cos(a)) * (u.DotProduct(v)) * u + Math.Sin(a) * (u.CrossProduct(v));

            rp = axis2.GetEndPoint(0) + vv * (p.DistanceTo(axis2.GetEndPoint(0)));

            return rp;
        }




        /// <summary>
        /// 判断点是否在区域内(区域边界线均为直线)
        /// </summary>
        /// <param name="array">区域边界线集合</param>
        /// <param name="point">需要判定的点</param>
        /// <param name="view">需要判定的视图</param>
        /// <returns></returns>
        public static bool PointInRegion(this CurveArray array, XYZ point, Autodesk.Revit.DB.View view)
        {
            XYZ xyz = GetPointOnPlane(point, view.Origin, view.ViewDirection);
            Line line = Line.CreateBound(xyz + view.RightDirection * 1e6, xyz - view.RightDirection * 1e6);

            List<XYZ> xyzlist = new List<XYZ>();
            xyzlist.Add(xyz);

            foreach (Curve curve in array)
            {
                Line line_1 = Line.CreateBound(GetPointOnPlane(curve.GetEndPoint(0), view.Origin, view.ViewDirection), GetPointOnPlane(curve.GetEndPoint(1), view.Origin, view.ViewDirection));
                XYZ xyz_1 = Line_IEresult(line, line_1);
                if (xyz_1 != null)
                {
                    bool buer = false;

                    if (xyzlist.Where(o => o.IsAlmostEqualTo(xyz_1)).Count() > 0) { buer = true; }

                    if (buer == false)
                    {
                        xyzlist.Add(xyz_1);
                    }

                }
            }

            List<XYZ> listing = OrderPointsOnOri(xyzlist, view.RightDirection);
            int d = 0;
            for (int c = 0; c < listing.Count; c++)
            {
                if (listing[c].IsAlmostEqualTo(xyz))
                {
                    d = c;
                    break;
                }
            }
            if (d % 2 == 1 && (listing.Count - 1 - d) % 2 == 1)
            {
                return true;
            }

            return false;
        }


    }
}
