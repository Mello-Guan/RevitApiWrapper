using System;
using System.Collections.Generic;
using System.Text;

namespace RevitApiWrapper.DB
{
    public static class ViewExtension
    {

        public static enum CheckViewSet
        {
            IsViewPlan,
            IsView3D,
            IsViewSection,
            NoView3D
        }


        /// <summary>
        /// 判断当前视图
        /// </summary>
        /// <param name="checkViewSet">判断设置</param>
        /// <param name="doc">文档</param>
        /// <param name="falseTip">判断为否时候的信息提示</param>
        /// <returns></returns>
        public static bool CheckView(this CheckViewSet checkViewSet,Document doc, string falseTip)
        {
            bool bur = false;
            Autodesk.Revit.DB.View view = doc.ActiveView;

            switch (checkViewSet)
            {
                case CheckViewSet.IsView3D:
                    if (view is View3D) { bur = true; }
                    else { bur = false; }
                    break;

                case CheckViewSet.IsViewPlan:
                    if (view is ViewPlan) { bur = true; }
                    else { bur = false; }
                    break;

                case CheckViewSet.IsViewSection:
                    if (view is ViewSection) { bur = true; }
                    else { bur = false; }
                    break;

                case CheckViewSet.NoView3D:
                    if (view is View3D) { bur = false; }
                    else { bur = true; }
                    break;

                default:
                    break;
            }

            if (bur == false)
            {
                MessageBox.Show(falseTip, "提示");
            }

            return bur;
        }




    }
}
