using Landis.Core;
using Landis.SpatialModeling;
using Landis.Library.UniversalCohorts;
using System.Collections.Generic;
using System.Dynamic;

namespace Landis.Extension.Succession.ForC
{
    public static class BBDHandler
    {
        public static void ProcessBBD()
        {
            IEnumerable<ActiveSite> sites = /* ModelCore.Landscape */PlugIn.ModelCore.Landscape.ActiveSites;
            
            // Dictionaries to store interfaces for site cohort data to provide O(1) access instead of native O(n)
            // This state only survives the current timestep and most of these dictionaries are cleared after every site iteration.
            Dictionary<ISpecies, ISpeciesCohorts> speciesLookup = new Dictionary<ISpecies, ISpeciesCohorts>();
            Dictionary<ISpecies, Dictionary<ushort, ICohort>> ageLookup = new Dictionary<ISpecies, Dictionary<ushort, ICohort>>();
            Dictionary<ISpecies, SpeciesCohorts> concreteLookup = new Dictionary<ISpecies, SpeciesCohorts>();
            Dictionary<ISpecies, Dictionary<ushort, int>> biomassTransfer = new Dictionary<ISpecies, Dictionary<ushort, int>>();
            
            // Define biomass transfer rules (source species -> target species)
            var biomassTransferRules = new Dictionary<string, string>
            {
                { "pinubank", "querelli" }
            };
            
            // Create species name to ISpecies mapping dictionary
            var speciesNameToISpecies = new Dictionary<string, ISpecies>();
            foreach (var species in PlugIn.ModelCore.Species) {
                speciesNameToISpecies[species.Name] = species;
            }
            

            
            foreach (ActiveSite site in sites) {
                SiteCohorts siteCohorts = SiteVars.Cohorts[site];
                // Stage 1: Build dictionaries for this site
                foreach (ISpeciesCohorts speciesCohorts in siteCohorts) {
                    speciesLookup[speciesCohorts.Species] = speciesCohorts;
                    
                    // Store concrete type for AddNewCohort access
                    concreteLookup[speciesCohorts.Species] = (SpeciesCohorts)speciesCohorts;
                    
                    // Build age lookup for O(1) age access within each species
                    var speciesAgeLookup = new Dictionary<ushort, ICohort>();
                    foreach (ICohort cohort in speciesCohorts) {
                        speciesAgeLookup[cohort.Data.Age] = cohort;
                    }
                    ageLookup[speciesCohorts.Species] = speciesAgeLookup;
                }
                
                // Debug output for specific site
                if (site.Location.Row == 7 && site.Location.Column == 5) {
                    foreach (ISpeciesCohorts speciesCohorts in siteCohorts) {
                        string speciesName = speciesCohorts.Species.Name;
                        foreach (ICohort cohort in speciesCohorts) {
                            if (speciesName == "pinubank" || speciesName == "querelli") {
                                PlugIn.ModelCore.UI.WriteLine($"Site: ({site.Location.Row},{site.Location.Column}), Species: {speciesName}, Age: {cohort.Data.Age}, Biomass: {cohort.Data.Biomass}");
                            }
                        }
                    }
                }
                
                // Stage 2: Biomass transfer logic using O(1) dictionary lookups
                
                // Look through ALL cohorts and check for pinubank
                foreach (ISpeciesCohorts speciesCohorts in siteCohorts) {
                    string speciesName = speciesCohorts.Species.Name;
                    foreach (ICohort cohort in speciesCohorts) {
                        if (biomassTransferRules.TryGetValue(speciesName, out string targetSpeciesName)) {
                            int transfer = (int)(cohort.Data.Biomass * 0.3);
                            cohort.ChangeBiomass(-transfer);
                            ISpecies targetSpecies = speciesNameToISpecies[targetSpeciesName];
                            if (!biomassTransfer.ContainsKey(targetSpecies)) {
                                biomassTransfer[targetSpecies] = new Dictionary<ushort, int>();
                            }
                            biomassTransfer[targetSpecies][cohort.Data.Age] = transfer;
                        }
                    }
                }
                
                // Process transfers to target species using O(1) lookups
                foreach (KeyValuePair<ISpecies, Dictionary<ushort, int>> speciesEntry in biomassTransfer) {
                    ISpecies targetSpecies = speciesEntry.Key;
                    
                    // O(1) species access
                    if (speciesLookup.TryGetValue(targetSpecies, out var targetCohorts)) {
                        foreach (KeyValuePair<ushort, int> ageEntry in speciesEntry.Value) {
                            ushort age = ageEntry.Key;
                            int transfer = ageEntry.Value;
                            
                            // O(1) age lookup
                            if (ageLookup[targetSpecies].TryGetValue(age, out var targetCohort)) {
                                targetCohort.ChangeBiomass(transfer);
                            } else {
                                // Use concrete type for O(1) AddNewCohort access
                                if (concreteLookup.TryGetValue(targetSpecies, out var concreteCohorts)) {
                                    concreteCohorts.AddNewCohort(age, transfer, new ExpandoObject());
                                } else {
                                    // Fallback to site-level O(n) AddNewCohort if species doesn't exist yet
                                    siteCohorts.AddNewCohort(targetSpecies, age, transfer, new ExpandoObject());
                                }
                            }
                        }
                    }
                }
                
                // Clear inner dictionaries for reuse
                foreach (var innerDict in biomassTransfer.Values) {
                    innerDict.Clear();
                }
                biomassTransfer.Clear();
                
                // Clear dictionaries for reuse at end of iteration
                speciesLookup.Clear();
                ageLookup.Clear();
                concreteLookup.Clear();
            }
        }
    }
} 