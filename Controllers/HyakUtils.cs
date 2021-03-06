﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Hyak.ApiModel;
using Hyak.ServiceModel;
using Newtonsoft.Json.Linq;

namespace ARMExplorer.Controllers
{
    public static class HyakUtils
    {
        static object _lock = new object();
        static Dictionary<Type, JArray[]> _operations = new Dictionary<Type, JArray[]>();

        static HyakUtils()
        {
            ServiceType.ResetDefinedTypes();
        }

        public static string CSMUrl
        {
            get;
            set;
        }

        public static JArray GetOperations<T>(bool hidden)
        {
            lock (_lock)
            {
                JArray[] cache;
                if (_operations.TryGetValue(typeof(T), out cache))
                {
                    return cache[hidden ? 1 : 0];
                }
            }

            var service = ApiModeler.Instantiate(typeof(T));

            JArray array = new JArray();
            JArray skip = new JArray();
            foreach (var method in service.Methods.Values)
            {
                GenerateMethod(ShouldSkip(method) ? skip : array, method);
            }

            foreach (var operation in service.ServiceOperations)
            {
                foreach (var method in operation.Value.Methods.Values)
                {
                    GenerateMethod(ShouldSkip(method) ? skip : array, method);
                }
            }

            lock (_lock)
            {
                _operations[typeof(T)] = new[] { array, skip };
            }

            return array;
        }

        private static bool ShouldSkip(IMethod method)
        {
            return method.Name.IndexOf("backup", StringComparison.OrdinalIgnoreCase) >= 0
                || method.Name.IndexOf("restore", StringComparison.OrdinalIgnoreCase) >= 0
                || method.Name.IndexOf("discover", StringComparison.OrdinalIgnoreCase) >= 0
                || method.Name.IndexOf("slotConfigNames", StringComparison.OrdinalIgnoreCase) >= 0
                || method.Name.IndexOf("metrics", StringComparison.OrdinalIgnoreCase) >= 0
                || method.Name.IndexOf("repository", StringComparison.OrdinalIgnoreCase) >= 0
                || method.Name.IndexOf("usages", StringComparison.OrdinalIgnoreCase) >= 0
                || method.Name.IndexOf("Clone", StringComparison.OrdinalIgnoreCase) >= 0
                || method.Name.IndexOf("GetOperation", StringComparison.OrdinalIgnoreCase) >= 0
                || method.Name.IndexOf("register", StringComparison.OrdinalIgnoreCase) >= 0
                || method.Name.IndexOf("unregister", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void GenerateMethod(JArray array, IMethod method)
        {
            var json = new JObject();
            json["MethodName"] = method.Name;
            json["HttpMethod"] = method.HttpMethod.ToString().ToUpper();

            if (method.RequestBodies.Count == 1)
            {
                var request = (RequestBody)method.RequestBodies.First().Value;
                var schema = GetJsonSchehma(request.SerializationFormat);
                json["RequestBody"] = schema;
            }

            if (method.ResponseBodies.Count == 1)
            {
                var response = (ResponseBody)method.ResponseBodies.First().Value.First().Value;
                var schema = GetJsonSchehma(response.SerializationFormat);
                json["ResponseBody"] = schema;
            }

            var url = EvaluateExpression(BindingExpression.Bind(method, method.UrlExpression)).ToString();
            url = url.Contains('?') ? url.Substring(0, url.IndexOf('?')) : url;

            var urls = new List<string>();
            if (url.Contains('['))
            {
                urls.Add(url.Replace("[", String.Empty).Replace("]", String.Empty));
                urls.Add(Regex.Replace(url, "[[][^]]*[]]+", String.Empty));
            }
            else
            {
                urls.Add(url);
            }

            foreach (var item in urls)
            {
                var clone = json.DeepClone();
                clone["Url"] = item;
                array.Add(clone);
            }
        }

        private static JToken GetJsonSchehma(ISerializationBase serialization)
        {
            var xmlValue = serialization as Hyak.ServiceModel.XmlElement;
            if (xmlValue != null)
            {
                return "(xml)";
            }

            var jsonValue = serialization as Hyak.ServiceModel.JsonValue;
            if (jsonValue != null)
            {
                var knownObjectType = jsonValue.Type as Hyak.ServiceModel.KnownObjectType;
                if (knownObjectType != null)
                {
                    var schema = new JObject();
                    foreach (var member in jsonValue.Members)
                    {
                        if ((member is JsonValue && ((JsonValue)member).PassThrough) ||
                            (member is JsonArray && ((JsonArray)member).PassThrough) ||
                            (member is JsonDictionary && ((JsonDictionary)member).PassThrough))
                        {
                            return GetJsonSchehma(member);
                        }

                        schema[member.Name] = GetJsonSchehma(member);
                    }
                    return schema;
                }

                var knownType = jsonValue.Type as Hyak.ServiceModel.KnownType;
                if (knownType != null)
                {
                    return GetJsonSchehma(knownType);
                }

                throw new InvalidOperationException("Should not reach here. jsonValue.Type  = " + jsonValue.Type);
            }

            var jsonArray = serialization as Hyak.ServiceModel.JsonArray;
            if (jsonArray != null)
            {
                var schema = new JArray();
                if (jsonArray.ElementFormat != null)
                {
                    schema.Add(GetJsonSchehma(jsonArray.ElementFormat));
                }
                else
                {
                    var knownType = jsonArray.Type.GenericParameters[0] as Hyak.ServiceModel.KnownType;
                    if (knownType != null)
                    {
                        schema.Add(GetJsonSchehma(knownType));
                    }
                    else
                    {
                        throw new InvalidOperationException("Should not reach here. array's elementType  = " + jsonArray.Type.GenericParameters[0]);
                    }
                }

                return schema;
            }

            var jsonDict = serialization as Hyak.ServiceModel.JsonDictionary;
            if (jsonDict != null)
            {
                return new JObject();
            }

            throw new InvalidOperationException("Should not reach here for " + serialization.GetType());
        }

        private static JToken GetJsonSchehma(Hyak.ServiceModel.KnownType knownType)
        {
            var enumType = knownType as Hyak.ServiceModel.EnumType;
            if (enumType != null)
            {
                return String.Format("({0})", String.Join("|", enumType.Values.Keys.ToArray()));
            }

            if (Nullable.GetUnderlyingType(knownType.UnderlyingType) != null)
            {
                return String.Format("({0})", knownType.UnderlyingType.GenericTypeArguments.First().Name.ToLowerInvariant());
            }

            return String.Format("({0})", knownType.UnderlyingType.Name.ToLowerInvariant());
        }

        private static object EvaluateExpression(BindingExpression expression)
        {
            if (expression.ToString().Equals("{BaseUri}", StringComparison.OrdinalIgnoreCase))
            {
                return CSMUrl;
            }

            var concat = expression as ConcatenatedBindingExpression;
            if (concat != null)
            {
                var strb = new StringBuilder();
                foreach (var item in concat.Expressions)
                {
                    strb.Append(EvaluateExpression(item));
                }
                return strb.ToString();
            }

            var conditional = expression as ConditionalBindingExpression;
            if (conditional != null)
            {
                var strb = new StringBuilder();
                strb.Append('[');
                strb.Append(EvaluateExpression(conditional.ConcatenatedExpression));
                strb.Append(']');
                return strb.ToString();
            }

            var literal = expression as LiteralBindingExpression;
            if (literal != null)
            {
                return literal.Text;
            }

            var instance = expression as InstanceBindingExpression;
            if (instance != null)
            {
                return instance.Context;
            }

            var property = expression as PropertyPathBindingExpression;
            if (property != null)
            {
                var obj = EvaluateExpression(property.Expression);
                if (obj == null)
                {
                    throw new InvalidOperationException(property.Expression + " expression should not be null");
                }

                var prop = obj.GetType().GetProperty(property.PropertyName);
                if (prop == null)
                {
                    return "{" + Char.ToLowerInvariant(property.PropertyName[0]) + property.PropertyName.Substring(1) + "}";
                }

                var value = prop.GetValue(obj);
                return value;
            }

            var parameter = expression as ParameterBindingExpression;
            if (parameter != null)
            {
                var name = parameter.ToString().Split(new[] { '.', '{', '}' }, StringSplitOptions.RemoveEmptyEntries).Last();
                return "{" + Char.ToLowerInvariant(name[0]) + name.Substring(1) + "}";
            }

            var formatting = expression as FormattingBindingExpression;
            if (formatting != null)
            {
                return "{" + formatting.FormatString + "}";
            }

            throw new InvalidOperationException("Should not reach here. " + expression.GetType() + ", " + expression);
        }
    }
}