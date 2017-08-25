﻿using Sitecore.Collections;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Form.Core.Attributes;
using Sitecore.Form.Core.Configuration;
using Sitecore.Form.Core.Utility;
using Sitecore.Form.Core.Visual;
using Sitecore.Resources;
using Sitecore.StringExtensions;
using Sitecore.WFFM.Abstractions.Dependencies;
using Sitecore.WFFM.Abstractions.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Sitecore.Support.Form.Core.Visual
{

    public class PropertiesFactory
    {
        // Fields
        [Obsolete("Use FieldSetEnd. Access modifier will be changed to private")]
        protected static string fieldSetEnd = "</fieldset>";
        [Obsolete("Use FieldSetStart. Access modifier will be changed to private")]
        protected static string fieldSetStart = "<fieldset class=\"sc-accordion-header\"><legend class=\"sc-accordion-header-left\"><span class=\"sc-accordion-header-center\">{0}<strong>{1}</strong><div class=\"sc-accordion-header-right\">&nbsp;</div></span></legend>";
        [Obsolete("Use Infos. Access modifier will be changed to private")]
        protected static Hashtable infos = new Hashtable();
        private readonly Item item;
        private readonly IItemRepository itemRepository;
        private readonly IResourceManager resourceManager;

        // Methods
        [Obsolete("Use another constructor")]
        public PropertiesFactory(Item item) : this(item, DependenciesManager.Resolve<IItemRepository>(), DependenciesManager.Resolve<IResourceManager>())
        {
        }

        public PropertiesFactory(Item item, IItemRepository itemRepository, IResourceManager resourceManager)
        {
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(itemRepository, "itemRepository");
            Assert.ArgumentNotNull(resourceManager, "resourceManager");
            this.item = item;
            this.itemRepository = itemRepository;
            this.resourceManager = resourceManager;
        }

        internal static IEnumerable<string> CompareTypes(IEnumerable<Pair<string, string>> properties, Item newType, Item oldType, ID assemblyField, ID classField)
        {
            Pair<string, string>[] source = (properties as Pair<string, string>[]) ?? properties.ToArray<Pair<string, string>>();
            if ((properties == null) || !source.Any<Pair<string, string>>())
            {
                return new string[0];
            }
            Func<Pair<string, string>, bool> predicate = null;
            List<VisualPropertyInfo> newTypeInfos = new PropertiesFactory(newType, DependenciesManager.Resolve<IItemRepository>(), DependenciesManager.Resolve<IResourceManager>()).GetProperties(assemblyField, classField);
            List<VisualPropertyInfo> oldTypeInfos = new PropertiesFactory(oldType, DependenciesManager.Resolve<IItemRepository>(), DependenciesManager.Resolve<IResourceManager>()).GetProperties(assemblyField, classField);
            IEnumerable<string> enumerable = new string[0];
            if (oldTypeInfos.Count > 0)
            {
                if (predicate == null)
                {
                    predicate = p => (oldTypeInfos.FirstOrDefault<VisualPropertyInfo>(s => (s.PropertyName.ToLower() == p.Part1.ToLower())) != null) && (oldTypeInfos.FirstOrDefault<VisualPropertyInfo>(s => (s.PropertyName.ToLower() == p.Part1.ToLower())).DefaultValue.ToLower() != p.Part2.ToLower());
                }
                enumerable = from p in source.Where<Pair<string, string>>(predicate) select p.Part1.ToLower();
            }
            return (from f in enumerable
                    where newTypeInfos.Find(s => s.PropertyName.ToLower() == f) == null
                    select oldTypeInfos.Find(s => s.PropertyName.ToLower() == f).DisplayName.TrimEnd(new char[] { ' ', ':' }));
        }

        protected VisualPropertyInfo[] GetClassDefinedProperties(ICustomAttributeProvider type)
        {
            List<VisualPropertyInfo> list = new List<VisualPropertyInfo>();
            if (type != null)
            {
                object[] customAttributes = type.GetCustomAttributes(typeof(VisualPropertiesAttribute), true);
                if (customAttributes.Length != 0)
                {
                    VisualPropertiesAttribute attribute = customAttributes[0] as VisualPropertiesAttribute;
                    if (attribute != null)
                    {
                        string[] properties = attribute.Properties;
                        list.AddRange(properties.Select<string, VisualPropertyInfo>(new Func<string, VisualPropertyInfo>(VisualPropertyInfo.Parse)));
                    }
                }
            }
            return list.ToArray();
        }

        private string GetDisplayNameAttributeValue(MemberInfo t)
        {
            DisplayNameAttribute customAttribute = t.GetCustomAttribute<DisplayNameAttribute>();
            if (customAttribute != null)
            {
                return customAttribute.DisplayName;
            }
            return t.Name;
        }

        protected VisualPropertyInfo[] GetMvcCustomErrorMessageProperties(Type type)
        {
            List<VisualPropertyInfo> list = new List<VisualPropertyInfo>();
            List<System.Attribute> list2 = new List<System.Attribute>();
            foreach (PropertyInfo info in type.GetProperties())
            {
                list2.AddRange(from a in System.Attribute.GetCustomAttributes(info)
                               where ((a.GetType().BaseType != typeof(Sitecore.Form.Core.Attributes.ValidationAttribute)) && (a.GetType().BaseType != typeof(System.ComponentModel.DataAnnotations.DataTypeAttribute))) && (a is Sitecore.Form.Core.Attributes.ValidationAttribute)
                               select a);
            }
            Dictionary<string, string> mvcValidationMessages = this.itemRepository.CreateFieldItem(this.item).MvcValidationMessages;
            list.AddRange(from g in from x in list2 group x by x.ToString()
                          select g.First<System.Attribute>() into a
                          select VisualPropertyInfo.Parse(a.GetType().Name, this.GetDisplayNameAttributeValue(a.GetType()), mvcValidationMessages.FirstOrDefault<KeyValuePair<string, string>>(m => (m.Key == a.GetType().Name)).Value, "VALIDATION_ERROR_MESSAGES", true));
            return list.ToArray();
        }

        protected List<VisualPropertyInfo> GetProperties(ID assemblyField, ID classField)
        {
            Assert.ArgumentNotNull(assemblyField, "assemblyField");
            Assert.ArgumentNotNull(classField, "classField");
            string key = this.item[assemblyField] + this.item[classField] + this.item[Sitecore.Form.Core.Configuration.FieldIDs.FieldUserControlID];
            if (Infos.ContainsKey(key))
            {
                return (Infos[key] as List<VisualPropertyInfo>);
            }
            List<VisualPropertyInfo> list = new List<VisualPropertyInfo>();
            Type type = FieldReflectionUtil.GetFieldType(this.item[assemblyField], this.item[classField], this.item[Sitecore.Form.Core.Configuration.FieldIDs.FieldUserControlID]);
            if (type != null)
            {
                list.AddRange(this.GetClassDefinedProperties(type));
                list.AddRange(this.GetPropertyDefinedProperties(type));
            }
            string str2 = this.item[Sitecore.Form.Core.Configuration.FieldIDs.MvcFieldId];
            if (!string.IsNullOrEmpty(str2))
            {
                Type type2 = Type.GetType(str2);
                if (type2 != null)
                {
                    list.AddRange(this.GetMvcCustomErrorMessageProperties(type2));
                }
            }
            list.Sort(new CategoryComparer());
            Infos.Add(key, list);
            return list;
        }

        protected VisualPropertyInfo[] GetPropertyDefinedProperties(Type type)
        {
            List<VisualPropertyInfo> list = new List<VisualPropertyInfo>();
            if (type != null)
            {
                PropertyInfo[] properties = type.GetProperties();
                list.AddRange(from property in properties.Select<PropertyInfo, VisualPropertyInfo>(new Func<PropertyInfo, VisualPropertyInfo>(VisualPropertyInfo.Parse))
                              where property != null
                              select property);
            }
            return list.ToArray();
        }

        protected string RenderCategoryBegin(string name)
        {
            ImageBuilder builder = new ImageBuilder
            {
                Width = 0x10,
                Height = 0x10,
                Border = "0",
                Align = "middle",
                Class = "sc-accordion-icon",
                Src = Themes.MapTheme("Applications/16x16/document_new.png", string.Empty, false)
            };
            object[] parameters = new object[] { builder.ToString(), Translate.Text(name) ?? string.Empty };
            return FieldSetStart.FormatWith(parameters);
        }

        protected string RenderCategoryEnd() =>
            FieldSetEnd;

        protected string RenderPropertiesEditor(IEnumerable<VisualPropertyInfo> properties)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<div class=\"scFieldProperties\" id=\"FieldProperties\" vAling=\"top\">");
            VisualPropertyInfo[] source = (properties as VisualPropertyInfo[]) ?? properties.ToArray<VisualPropertyInfo>();
            if (!source.Any<VisualPropertyInfo>())
            {
                builder.Append("<div class=\"scFbSettingSectionEmpty\">");
                builder.AppendFormat("<label class='scFbHasNoPropLabel'>{0}</label>", this.resourceManager.Localize("HAS_NO_PROPERTIES"));
                builder.Append("</div>");
            }
            string category = string.Empty;
            bool flag = false;
            foreach (VisualPropertyInfo info in source)
            {
                if (string.IsNullOrEmpty(category) || (category != info.Category))
                {
                    if (flag)
                    {
                        builder.Append("</div>");
                        builder.Append(this.RenderCategoryEnd());
                    }
                    category = info.Category;
                    flag = true;
                    builder.Append(this.RenderCategoryBegin(category));
                    builder.Append("<div class='sc-accordion-field-body'>");
                }
                builder.Append(this.RenderProperty(info));
            }
            builder.Append("</div>");
            return builder.ToString();
        }

        protected string RenderPropertiesEditor(ID assemblyField, ID classField) =>
            this.RenderPropertiesEditor(this.GetProperties(assemblyField, classField));

        public static string RenderPropertiesSection(Item item, ID assemblyField, ID classField) =>
            new PropertiesFactory(item, DependenciesManager.Resolve<IItemRepository>(), DependenciesManager.Resolve<IResourceManager>()).RenderPropertiesEditor(assemblyField, classField);

        protected string RenderProperty(VisualPropertyInfo info)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<div class='scFbPeEntry'>");
            if (!string.IsNullOrEmpty(info.DisplayName))
            {
                string str = Translate.Text(info.DisplayName);
                string str2 = (info.FieldType is EditField) ? "scFbPeLabelFullWidth" : "scFbPeLabel";
                builder.AppendFormat("<label class='{0}' for='{1}'>{2}</label>", str2, info.ID, str);
            }
            builder.Append(info.RenderField());
            builder.Append("</div>");
            return builder.ToString();
        }

        // Properties
        public static string FieldSetEnd
        {
            get
            {
                return PropertiesFactory.fieldSetEnd;
            }
            set
            {
                PropertiesFactory.fieldSetEnd = value;
            }
        }

        public static string FieldSetStart
        {
            get
            {
                return PropertiesFactory.fieldSetStart;
            }
            set
            {
                PropertiesFactory.fieldSetStart = value;
            }
        }

        protected static Hashtable Infos
        {
            get
            {
                return PropertiesFactory.infos;
            }
            set
            {
                PropertiesFactory.infos = value;
            }
        }       
}

}
