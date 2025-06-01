using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scriban;
using Scriban.Syntax;
using UnityEngine;

namespace Code
{
    public class TemplateManager : MonoBehaviour
    {
        public static TemplateManager Instance { get; private set; }

        private Dictionary<string, Template> _templates = new();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }

            TextAsset[] allTemplates = Resources.LoadAll<TextAsset>(TemplateDirectory);
            foreach (TextAsset template in allTemplates)
            {
                string templateName = template.name;
                _templates[templateName] = LoadTemplate(template.text);
            }
        }

        private const string TemplateDirectory = "PromptTemplates";

        /// <summary>
        /// render a template with the given name and a data object
        /// </summary>
        /// <param name="templateName"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public string Render(string templateName, object data)
        {
            if (!_templates.ContainsKey(templateName))
            {
                throw new Exception("Invalid template name: " + templateName);
            }

            var template = _templates[templateName];
            return template.Render(data, member => member.Name);
        }

        /// <summary>
        /// render a template with the given name and a dictionary of data
        /// </summary>
        /// <param name="templateName"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public string Render(string templateName, Dictionary<string, object> data)
        {
            var template = LoadTemplate(templateName);
            var scriptObj = new Scriban.Runtime.ScriptObject();
            foreach (var kv in data)
            {
                scriptObj.Add(kv.Key, kv.Value);
            }

            var context = new Scriban.TemplateContext();
            context.PushGlobal(scriptObj);

            return template.Render(context);
        }

        private Template LoadTemplate(string content)
        {
            string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string filtered = string.Join("\n", lines.Where(line => !line.TrimStart().StartsWith("#")));

            var template = Template.Parse(filtered);

            if (template.HasErrors)
            {
                throw new InvalidOperationException(
                    "Template parsing errors:\n" + string.Join("\n", template.Messages.Select(m => m.Message))
                );
            }

            return template;
        }
    }
}