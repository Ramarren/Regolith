using System;
using System.Collections.Generic;
using FinePrint;
using Regolith.Asteroids;
using Regolith.Common;
using UnityEngine;

namespace Regolith.Converters
{
    public abstract class BaseConverter : PartModule, IAnimatedModule
    {
        [KSPField(isPersistant = true)]
        public float EfficiencyBonus = 1;
        
        [KSPField(isPersistant = true)]
        public bool IsActivated = false;

        [KSPField(isPersistant = true)]
        public bool DirtyFlag = true;
        
        [KSPField(isPersistant = false)]
        public string StartActionName;

        [KSPField(isPersistant = false)]
        public string StopActionName;

        [KSPField(isPersistant = false)]
        public string ConverterName = "";

        [KSPField] 
        public float FillAmount = 1;

        [KSPField]
        public float TakeAmount = 1;

        
        [KSPField(guiActive = true, guiName = "", guiActiveEditor = false)]
        public string status = "Inactive";

        [KSPEvent(guiActive = true, guiName = "Start Converter", active = false)]
        public void StartResourceConverter()
        {
            IsActivated = true;
        }

        [KSPEvent(guiActive = true, guiName = "Stop Converter", active = false)]
        public void StopResourceConverter()
        {
            IsActivated = false;
            status = "Inactive";
        }

        [KSPAction("Stop Converter")]
        public void StopResourceConverterAction(KSPActionParam param)
        {
            StopResourceConverter();
        }

        [KSPAction("Start Converter")]
        public void StartResourceConverterAction(KSPActionParam param)
        {
            StartResourceConverter();
        }

        protected double lastUpdateTime;
        protected IResourceBroker _broker;
        protected ResourceConverter _converter;

        protected BaseConverter()
        {
            _broker = new ResourceBroker();
            _converter = new ResourceConverter(_broker);
        }

        public void SetEfficiencyBonus(float bonus)
        {
            EfficiencyBonus = bonus;
        }

        public void EnableModule()
        {
            isEnabled = true;
        }

        public void DisableModule()
        {
            isEnabled = false;
            IsActivated = false;
        }

        public bool ModuleIsActive()
        {
            return IsActivated;
        }

        protected double GetDeltaTime()
        {
            try
            {
                if (Time.timeSinceLevelLoad < 1.0f || !FlightGlobals.ready)
                {
                    return -1;
                }

                if (Math.Abs(lastUpdateTime) < Utilities.FLOAT_TOLERANCE)
                {
                    // Just started running
                    lastUpdateTime = Planetarium.GetUniversalTime();
                    return -1;
                }

                var deltaTime = Math.Min(Planetarium.GetUniversalTime() - lastUpdateTime, Utilities.GetMaxDeltaTime());
                lastUpdateTime += deltaTime;
                return deltaTime;
            }
            catch (Exception e)
            {
                print("[REGO] - Error in - BaseConverter_GetDeltaTime - " + e.Message);
                return 0;
            }
        }

        protected void UpdateConverterStatus()
        {
            if (DirtyFlag != IsActivated)
            {
                DirtyFlag = IsActivated;
                Events["StartResourceConverter"].active = !IsActivated;
                Events["StopResourceConverter"].active = IsActivated;
                if (IsActivated)
                {
                    status = "Operational";
                }
                MonoUtilities.RefreshContextWindows(part);
            }            
        }

        public override void OnLoad(ConfigNode node)
        {
            if (vessel == null)
                return;
            try
            {
                base.OnLoad(node);
                lastUpdateTime = Utilities.GetValue(node, "lastUpdateTime", lastUpdateTime);
                SetupModule();
            }

            catch (Exception e)
            {
                print("[REGO] - Error in - BaseConverter_OnLoad - " + e.Message); 
            }
        }

        private void SetupModule()
        {
            part.force_activate();
            Events["StartResourceConverter"].guiName = StartActionName;
            Events["StopResourceConverter"].guiName = StopActionName;
            Actions["StartResourceConverterAction"].guiName = StartActionName;
            Actions["StopResourceConverterAction"].guiName = StopActionName;
            Fields["status"].guiName = ConverterName;
            //Fields["load"].guiName = ConverterName + " Load";
            //Check for presence of an Animation Group.  If not present, enable the module.

            if (!part.Modules.Contains("REGO_ModuleAnimationGroup"))
            {
                EnableModule();
            }
        }

        public override void OnStart(StartState state)
        {
            if (vessel == null)
                return;
            try
            {
                base.OnStart(state);
                SetupModule();
            }

            catch (Exception e)
            {
                print("[REGO] - Error in - BaseConverter_OnStart - " + e.Message);
            }
        }


        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            node.AddValue("lastUpdateTime", lastUpdateTime);
        }

        public override void OnFixedUpdate()
        {
            try
            {
                //Handle state change
                UpdateConverterStatus();
                if (IsActivated)
                {
                    //Check our time
                    var deltaTime = GetDeltaTime();
                    if (deltaTime < 0) return;
                    var recipe = PrepareRecipe(deltaTime);
                    //To support trickle charging
                    if (recipe != null)
                    {
                        recipe.FillAmount = FillAmount;
                        recipe.TakeAmount = TakeAmount;
                        var result = _converter.ProcessRecipe(deltaTime, recipe, part, EfficiencyBonus);
                        PostProcess(result, deltaTime);
                    }
                }
                PostUpdateCleanup();
            }
            catch (Exception e)
            {
                print("[REGO] - Error in - BaseConverter_OnFixedUpdate - " + e.Message); 
            }
        }

        protected virtual void PostUpdateCleanup()
        {
            //Runs regardless of generator state.
        }

        protected virtual void PostProcess(ConverterResults result, double deltaTime)
        {
            var statString = String.Format("{0:0.00}% load", result.TimeFactor/deltaTime*100);
            if (result.TimeFactor <= Utilities.FLOAT_TOLERANCE)
            {
                status = result.Status;
            }
            else
            {
                status = statString;
            }
        }

        protected virtual ConversionRecipe PrepareRecipe(double deltatime)
        {
            print("[REGOLITH] No Implementation of PrepareRecipe in derived class");
            return null;
        }
    }
}