﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using Buildalyzer;
using TechTalk.SpecFlow.Configuration;
using TechTalk.SpecFlow.Generator.Configuration;
using TechTalk.SpecFlow.Generator.Interfaces;

namespace TechTalk.SpecFlow.Generator.Project
{
    public class BuildalyzerProjectReader : ISpecFlowProjectReader
    {
        private readonly IGeneratorConfigurationProvider _configurationLoader;

        public BuildalyzerProjectReader(IGeneratorConfigurationProvider configurationLoader)
        {
            _configurationLoader = configurationLoader;
        }

        public SpecFlowProject ReadSpecFlowProject(string projectFilePath)
        {
            var manager = new AnalyzerManager();
            var analyzer = manager.GetProject(projectFilePath);
            var project = analyzer.Project;

            var projectFolder = Path.GetDirectoryName(projectFilePath);

            var specFlowProject = new SpecFlowProject();
            specFlowProject.ProjectSettings.ProjectFolder = projectFolder;
            specFlowProject.ProjectSettings.ProjectName = Path.GetFileNameWithoutExtension(projectFilePath);
            specFlowProject.ProjectSettings.AssemblyName = project.Properties.First(x => x.Name == "AssemblyName").EvaluatedValue;
            specFlowProject.ProjectSettings.DefaultNamespace = project.Properties.First(x => x.Name == "RootNamespace").EvaluatedValue;
            specFlowProject.ProjectSettings.ProjectPlatformSettings.Language = GetLanguage(project);

            foreach (var item in project.FeatureFiles())
            {
                var featureFile = specFlowProject.GetOrCreateFeatureFile(item.EvaluatedInclude);
                var ns = item.GetMetadataValue("CustomToolNamespace");
                if (!String.IsNullOrEmpty(ns))
                    featureFile.CustomNamespace = ns;
                if (!specFlowProject.FeatureFiles.Contains(featureFile))
                {
                    specFlowProject.FeatureFiles.Add(featureFile);
                }
            }


            specFlowProject.ProjectSettings.ConfigurationHolder = GetSpecFlowConfigurationHolder(project, projectFolder);

            if (specFlowProject.ProjectSettings.ConfigurationHolder != null)
            {
                specFlowProject.Configuration = _configurationLoader.LoadConfiguration(specFlowProject.ProjectSettings.ConfigurationHolder);
            }

            return specFlowProject;
        }

        private SpecFlowConfigurationHolder GetSpecFlowConfigurationHolder(Microsoft.Build.Evaluation.Project project, string projectFolder)
        {
            var jsonConfig = project.SpecFlowJsonConfigurationFile();
            if (jsonConfig != null)
            {
                var configFilePath = Path.Combine(projectFolder, jsonConfig.EvaluatedInclude);
                var configFileContent = File.ReadAllText(configFilePath);
                return new SpecFlowConfigurationHolder(ConfigSource.Json, configFileContent);
            }
            else
            {
                var appConfigItem = project.ApplicationConfigurationFile();
                if (appConfigItem != null)
                {
                    var configFilePath = Path.Combine(projectFolder, appConfigItem.EvaluatedInclude);
                    var configFileContent = File.ReadAllText(configFilePath);
                    return GetConfigurationHolderFromFileContent(configFileContent);
                }
            }
            return null;
        }

        private string GetLanguage(Microsoft.Build.Evaluation.Project project)
        {
            if (project.Imports.Any(i => i.ImportingElement.Project.Contains("Microsoft.VisualBasic.targets")))
                return GenerationTargetLanguage.VB;

            if (project.Imports.Any(i => i.ImportingElement.Project.Contains("Microsoft.CSharp.targets")))
                return GenerationTargetLanguage.CSharp;

            return GenerationTargetLanguage.CSharp;
        }

        private static SpecFlowConfigurationHolder GetConfigurationHolderFromFileContent(string configFileContent)
        {
            try
            {
                var configDocument = new XmlDocument();
                configDocument.LoadXml(configFileContent);

                return new SpecFlowConfigurationHolder(configDocument.SelectSingleNode("/configuration/specFlow"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex, "Config load error");
                return new SpecFlowConfigurationHolder();
            }
        }
    }
}