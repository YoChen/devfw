﻿using System;
using System.Globalization;
using System.IO;
using System.Web;
using System.Web.Routing;
using AtNet.DevFw.Framework.Xml.AutoObject;
using AtNet.DevFw.PluginKernel;
using AtNet.DevFw.PluginKernel.Web;
using AtNet.DevFw.Template;

namespace AtNet.DevFw.Web.Plugins
{

   

    /// <summary>
    /// CMS插件
    /// </summary>
    [PluginHost("扩展模块插件", "使用{module}.sh/{action}访问自定义扩展")]
    internal class ExtendsPluginApp : BaseExtendPluginHost,IExtendApp
    {
        private bool _loaded;
        private string _fpath;
        private string _shareJs;
        private string _shareCss;
        private string _pluginPath;


        internal ExtendsPluginApp(PluginWebHandleProxy<HttpContext> webHandler)
            : base(webHandler)
        {
            SavePluginMetadataToXml();
        }

        /// <summary>
        /// 扩展模块GET请求,返回false则应立即截断请求
        /// </summary>
        /// <param name="context"></param>
        /// <param name="extendName"></param>
        /// <param name="result"></param>
        public override void ExtendModuleRequest(HttpContext context, string extendName,ref bool result)
        {
            extendName = extendName.ToLower();

            //处理扩展
            this.WebHandler.HandleGetRequest(context, extendName, ref result);
        }


        /// <summary>
        /// 扩展模块POST请求,返回false则应立即截断请求
        /// </summary>
        /// <param name="context"></param>
        /// <param name="extendName"></param>
        /// <param name="result"></param>
        public override void ExtendModulePost(HttpContext context, string extendName,ref bool result)
        {
            extendName = extendName.ToLower();

            //处理扩展
            this.WebHandler.HandlePostRequest(context, extendName, ref result);
        }

        /// <summary>
        /// 保存插件数据到XML文件中
        /// </summary>
        public  void SavePluginMetadataToXml()
        {
            string fileName = String.Concat(FwCtx.PhysicalPath, FwCtx.Variables.TempPath, "plugin_meta.xml");

            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            AutoObjectXml xml = new AutoObjectXml(fileName);

            base.Iterate((p, a) =>
            {
                xml.InsertObjectNode(a.WorkIndent, a.Name, a.Description,
                                     new XmlObjectProperty("version", "版本", a.Version),
                                     new XmlObjectProperty("state", "状态", ((int)a.State).ToString()),
                                     new XmlObjectProperty("author", "作者", a.Author),
                                     new XmlObjectProperty("icon", "图标", a.Icon),
                                     new XmlObjectProperty("webpage", "官网", a.WebPage),
                                     new XmlObjectProperty("portalUrl", "入口地址", a.PortalUrl),
                                     new XmlObjectProperty("configUrl", "设置地址", a.ConfigUrl)
                                    );

            });
            xml.Flush();
        }

        /// <summary>
        /// 获取插件的图标
        /// </summary>
        /// <param name="workerIndent"></param>
        /// <returns></returns>
        public byte[] GetPluginIcon(string workerIndent, int width, int height)
        {
            return PluginUtil.GetPluginIcon(workerIndent, width, height, 
                String.Concat(FwCtx.PhysicalPath, FwCtx.Variables.PLUGIN_DEFAULT_ICON));
        }


        #region IExtendApp 

        /// <summary>
        /// 获取模板页
        /// </summary>
        /// <param name="filePath">相对于插件目录的文件路径，如插件com.demo引用模板test.html,将test.html放在/plugins/com.demo/下就可以了</param>
        /// <returns></returns>
        [Obsolete]
        public TemplatePage GetPage<T>(string filePath) where T : IPlugin
        {
            this.Load();
            PluginPackAttribute attr = PluginUtil.GetPluginByType(typeof(T)).GetAttribute();
            return this.GetTemplatePage(filePath, attr);
        }

        /// <summary>
        /// 获取模板页
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="filePath">相对于插件目录的文件路径，如插件com.demo引用模板test.html,将test.html放在/plugins/com.demo/下就可以了</param>
        /// <returns></returns>
        public TemplatePage GetPage(IPlugin plugin, string filePath)
        {
            this.Load();

            PluginPackAttribute attr = plugin.GetAttribute();
            return this.GetTemplatePage(filePath, attr);
        }

        public TemplatePage GetTemplatePage(string filePath, PluginPackAttribute attr)
        {
            string pluginDirPath = attr.WorkSpace;


            string cacheId = String.Concat("plugin", "_tpl_", attr.WorkIndent, filePath);
            string html = WebCtx.Cache.Get(cacheId) as string;

            if (html == null)
            {
                //模板文件放在/plugins/com.spdepository/pages/下
                string tplFilePath = pluginDirPath + filePath;

                try
                {
                    using (TextReader tr = new StreamReader(tplFilePath))
                    {
                        html = tr.ReadToEnd();
                        tr.Dispose();
                    }

                    WebCtx.Cache.Insert(cacheId, html, tplFilePath);
                }
                catch (Exception exc)
                {
                    throw new Exception(exc.Message + ", File:" + tplFilePath);
                }

            }

            TemplatePage tpl = new TemplatePage();
            tpl.TemplateContent = html;

            string pluginPath = this._pluginPath + attr.WorkIndent;

            tpl.AddVariable("os", new
            {
                version = FwCtx.Version.GetVersion(),
                domain = WebCtx.Domain,
                ppath = pluginPath,
                fpath = this._fpath,
                mjs = this._shareJs,
                sjs = this._shareJs,
                scss = this._shareCss,
                mcss = this._shareCss
            });

            tpl.AddVariable("plugin", new
            {
                path = pluginPath,
                indent = attr.WorkIndent,
                name = attr.Name
            });

            return tpl;
        }

        private void Load()
        {
            if (!this._loaded)
            {
                string domain = WebCtx.Domain;
                this._fpath = String.Concat(domain, "/", FwCtx.Variables.AssetsPath.Substring(0, FwCtx.Variables.AssetsPath.Length - 1));
                this._shareJs = String.Concat(domain, "/",FwCtx.Variables.AssetsPath, "share.js?ver=", FwCtx.Version.GetVersion());
                this._shareCss = String.Concat(domain,"/", FwCtx.Variables.AssetsPath, "share.css?ver=", FwCtx.Version.GetVersion());
                this._pluginPath =String.Concat( domain ,"/",PluginConfig.PLUGIN_DIRECTORY);
                this._loaded = true;
            }
        }


        /// <summary>
        /// 在获取用户失败的情况下，则调用此方法
        /// </summary>
        /// <param name="context"></param>
        public void LoadSession(System.Web.HttpContext context)
        {

            //todo: load sesion
//            context.Response.Write("<script>location.href='/" +
//                Settings.SYS_ADMIN_TAG +
//                "?ls" +
//                "';</script>");
//            context.Response.End();
        }


        /// <summary>
        /// 注册插件路由
        /// </summary>
        /// <param name="aliasName">别名</param>
        /// <param name="workIndent">插件标识</param>
        private static void MapExtendPluginRoute(string aliasName, string workIndent)
        {
            RouteValueDictionary routeValue = new RouteValueDictionary();
            routeValue.Add("extend", workIndent);

            RouteTable.Routes.Add("extend_sh_" + aliasName,
                                  new Route(aliasName + "/{*path}",
                                  routeValue,
                                  new PluginExtendRouteHandler()));

            if (FwCtx.Mono())
            {
                RouteTable.Routes.Add("mono_extend_sh_" + aliasName,
                                  new Route(aliasName,
                                  routeValue,
                                  new PluginExtendRouteHandler()));
            }
        }

        /// <summary>
        /// 注册插件路由
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="aliasName"></param>
        public void MapExtendPluginRoute(IPlugin plugin, string aliasName)
        {
            PluginPackAttribute attr = PluginUtil.GetAttribute(plugin);
            string workIndent = attr.WorkIndent;
            MapExtendPluginRoute( aliasName, workIndent);
        }

        /// <summary>
        /// 注册插件路由
        /// </summary>
        /// <param name="plugin"></param>
        public  void MapExtendPluginRoute(IPlugin plugin)
        {
            string aliasName;
            string workIndent;

            PluginPackAttribute attr = PluginUtil.GetAttribute(plugin);
            workIndent = attr.WorkIndent;
            aliasName = attr.Settings["override.url.indent"];

            //throw new Exception(extendName + "/" + aliasName);
            if (aliasName == ""
                || String.Compare(aliasName,
                workIndent, true,
                CultureInfo.InvariantCulture) == 0)
                return;
            MapExtendPluginRoute(aliasName, workIndent);
        }

        #endregion
    }
}
