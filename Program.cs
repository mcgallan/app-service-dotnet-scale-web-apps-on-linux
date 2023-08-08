// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.TrafficManager;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.Samples.Common;
using System;

namespace ManageLinuxWebAppWithTrafficManager
{
    /**
     * Azure App Service sample for managing web apps.
     *  - Create a domain
     *  - Create a self-signed certificate for the domain
     *  - Create 3 app service plans in 3 different regions
     *  - Create 5 web apps under the 3 plans, bound to the domain and the certificate
     *  - Create a traffic manager in front of the web apps
     *  - Scale up the app service plans to twice the capacity
     */

    public class Program
    {
        private static string CERT_PASSWORD = Utilities.CreatePassword();
        private static string pfxPath;

        public static void RunSample(ArmClient client)
        {
            AzureLocation region = AzureLocation.EastUS;
            string resourceGroupName = SdkContext.RandomResourceName("rgNEMV_", 24);
            string app1Name = SdkContext.RandomResourceName("webapp1-", 20);
            string app2Name = SdkContext.RandomResourceName("webapp2-", 20);
            string app3Name = SdkContext.RandomResourceName("webapp3-", 20);
            string app4Name = SdkContext.RandomResourceName("webapp4-", 20);
            string app5Name = SdkContext.RandomResourceName("webapp5-", 20);
            string plan1Name = SdkContext.RandomResourceName("jplan1_", 15);
            string plan2Name = SdkContext.RandomResourceName("jplan2_", 15);
            string plan3Name = SdkContext.RandomResourceName("jplan3_", 15);
            string domainName = SdkContext.RandomResourceName("jsdkdemo-", 20) + ".com";
            string trafficManagerName = SdkContext.RandomResourceName("jsdktm-", 20);
            var lro = client.GetDefaultSubscription().GetResourceGroups().CreateOrUpdate(Azure.WaitUntil.Completed, resourceGroupName, new ResourceGroupData(AzureLocation.EastUS));
            var resourceGroup = lro.Value;

            try
            {
                //============================================================
                // Purchase a domain (will be canceled for a full refund)

                Utilities.Log("Purchasing a domain " + domainName + "...");

                AppServiceDomainCollection domainCollection = resourceGroup.GetAppServiceDomains();
                var domainData = new AppServiceDomainData(AzureLocation.EastUS)
                {
                    ContactRegistrant = new Azure.ResourceManager.AppService.Models.RegistrationContactInfo("jondoe@contoso.com", "Jon", "Doe", "4258828080")
                    {
                        AddressMailing = new Azure.ResourceManager.AppService.Models.RegistrationAddressInfo("123 4th Ave", "Redmond", "United States", "98052", "WA")
                    },
                    IsDomainPrivacyEnabled = true,
                    IsAutoRenew = false
                };
                var domain_lro = domainCollection.CreateOrUpdate(Azure.WaitUntil.Completed, domainName, domainData);
                var domain = domain_lro.Value;
                Utilities.Log("Purchased domain " + domain.Data.Name);
                Utilities.Print(domain);

                //============================================================
                // Create a self-singed SSL certificate

                pfxPath = "webapp_" + nameof(ManageLinuxWebAppWithTrafficManager).ToLower() + ".pfx";

                Utilities.Log("Creating a self-signed certificate " + pfxPath + "...");

                Utilities.CreateCertificate(domainName, pfxPath, CERT_PASSWORD);

                //============================================================
                // Create 3 app service plans in 3 regions

                Utilities.Log("Creating app service plan " + plan1Name + " in US West...");

                var plan1 = CreateAppServicePlan(resourceGroup, plan1Name, AzureLocation.WestUS);

                Utilities.Log("Created app service plan " + plan1.Data.Name);
                Utilities.Print(plan1);

                Utilities.Log("Creating app service plan " + plan2Name + " in Europe West...");

                var plan2 = CreateAppServicePlan(resourceGroup, plan2Name, AzureLocation.WestEurope);

                Utilities.Log("Created app service plan " + plan2.Data.Name);
                Utilities.Print(plan1);

                Utilities.Log("Creating app service plan " + plan3Name + " in Asia South East...");

                var plan3 = CreateAppServicePlan(resourceGroup, plan3Name, AzureLocation.SoutheastAsia);

                Utilities.Log("Created app service plan " + plan2.Data.Name);
                Utilities.Print(plan1);

                //============================================================
                // Create 5 web apps under these 3 app service plans

                Utilities.Log("Creating web app " + app1Name + "...");

                var app1 = CreateWebApp(resourceGroup, app1Name, plan1.Data.Id, region);

                Utilities.Log("Created web app " + app1.Data.Name);
                Utilities.Print(app1);

                Utilities.Log("Creating another web app " + app2Name + "...");
                var app2 = CreateWebApp(resourceGroup, app2Name, plan2.Id, region);

                Utilities.Log("Created web app " + app2.Data.Name);
                Utilities.Print(app2);

                Utilities.Log("Creating another web app " + app3Name + "...");
                var app3 = CreateWebApp(resourceGroup, app3Name, plan3.Id, region);

                Utilities.Log("Created web app " + app3.Data.Name);
                Utilities.Print(app3);

                Utilities.Log("Creating another web app " + app3Name + "...");
                var app4 = CreateWebApp(resourceGroup, app4Name, plan1.Id, region);

                Utilities.Log("Created web app " + app4.Data.Name);
                Utilities.Print(app4);

                Utilities.Log("Creating another web app " + app3Name + "...");
                var app5 = CreateWebApp(resourceGroup, app5Name, plan1.Id, region);

                Utilities.Log("Created web app " + app5.Data.Name);
                Utilities.Print(app5);

                //============================================================
                // Create a traffic manager

                Utilities.Log("Creating a traffic manager " + trafficManagerName + " for the web apps...");

                var trafficManagerProfileCollection = resourceGroup.GetTrafficManagerProfiles();
                var profileData = new TrafficManagerProfileData()
                {
                    TrafficRoutingMethod = Azure.ResourceManager.TrafficManager.Models.TrafficRoutingMethod.Weighted,
                    Endpoints =
                    {
                        new TrafficManagerEndpointData() 
                        {
                        TargetResourceId = app1.Id
                        },
                        new TrafficManagerEndpointData()
                        {
                        TargetResourceId = app2.Id
                        },
                        new TrafficManagerEndpointData()
                        {
                        TargetResourceId = app3.Id
                        },
                    }
                };
                var profile_lro = trafficManagerProfileCollection.CreateOrUpdate(Azure.WaitUntil.Completed, trafficManagerName, profileData);
                var trafficManager = profile_lro.Value;

                Utilities.Log("Created traffic manager " + trafficManager.Data.Name);

                //============================================================
                // Scale up the app service plans

                Utilities.Log("Scaling up app service plan " + plan1Name + "...");

                plan1.Update(new Azure.ResourceManager.AppService.Models.AppServicePlanPatch()
                {
                    TargetWorkerCount = plan1.Data.TargetWorkerCount*2,
                });

                Utilities.Log("Scaled up app service plan " + plan1Name);
                Utilities.Print(plan1);

                Utilities.Log("Scaling up app service plan " + plan2Name + "...");

                plan2.Update(new Azure.ResourceManager.AppService.Models.AppServicePlanPatch()
                {
                    TargetWorkerCount = plan2.Data.TargetWorkerCount * 2,
                });

                Utilities.Log("Scaled up app service plan " + plan2Name);
                Utilities.Print(plan2);

                Utilities.Log("Scaling up app service plan " + plan3Name + "...");

                plan3.Update(new Azure.ResourceManager.AppService.Models.AppServicePlanPatch()
                {
                    TargetWorkerCount = plan3.Data.TargetWorkerCount * 2,
                });

                Utilities.Log("Scaled up app service plan " + plan3Name);
                Utilities.Print(plan3);
            }
            finally
            {
                try
                {
                    Utilities.Log("Deleting Resource Group: " + resourceGroupName);
                    resourceGroup.Delete(Azure.WaitUntil.Completed);
                    Utilities.Log("Deleted Resource Group: " + resourceGroupName);
                }
                catch (NullReferenceException)
                {
                    Utilities.Log("Did not create any resources in Azure. No clean up is necessary");
                }
                catch (Exception g)
                {
                    Utilities.Log(g);
                }
            }
        }

        public static void Main(string[] args)
        {
            try
            {
                //=================================================================
                // Authenticate
                var credentials = SdkContext.AzureCredentialsFactory.FromFile(Environment.GetEnvironmentVariable("AZURE_AUTH_LOCATION"));

                var client = new ArmClient(null, "db1ab6f0-4769-4b27-930e-01e2ef9c123c");

                // Print selected subscription
                Utilities.Log("Selected subscription: " + client.GetSubscriptions().Id);

                RunSample(client);
            }
            catch (Exception e)
            {
                Utilities.Log(e);
            }
        }

        private static WebSiteResource CreateWebApp(ResourceGroupResource resourceGroup, string name,ResourceIdentifier planId , AzureLocation region)
        {

            var webSiteCollection = resourceGroup.GetWebSites();
            var webSiteData = new WebSiteData(region)
            {
                SiteConfig = new Azure.ResourceManager.AppService.Models.SiteConfigProperties()
                {
                    WindowsFxVersion = "PricingTier.StandardS1",
                    NetFrameworkVersion = "NetFrameworkVersion.V4_6",
                },
                AppServicePlanId = planId,
            };
            var webSite_lro = webSiteCollection.CreateOrUpdate(Azure.WaitUntil.Completed, name, webSiteData);
            var webSite = webSite_lro.Value;
            return webSite;
        }

        private static AppServicePlanResource CreateAppServicePlan(ResourceGroupResource resourceGroup, string name, AzureLocation region)
        {
            var appServiceCollection = resourceGroup.GetAppServicePlans();
            var planData = new AppServicePlanData(region)
            {
            };
            var plan_lro = appServiceCollection.CreateOrUpdate(Azure.WaitUntil.Completed, name, planData);
            var plan = plan_lro.Value;
            return plan;
        }
    }
}