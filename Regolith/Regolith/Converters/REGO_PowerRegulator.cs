using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Regolith.Common;

namespace Regolith.Converters
{
    class REGO_PowerRegulator : PartModule
    {
        [KSPField]
        public float baseAmount = 1;
        
        [KSPField(isPersistant = true)]
        public float currentMultiplier = 0.02f;
        
        protected double lastUpdateTime;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (vessel != null){
                try
                {
                    lastUpdateTime = Utilities.GetValue(node, "lastUpdateTime", lastUpdateTime);
                    updateMultipliers();
                }

                catch (Exception e)
                {
                    print("[REGO] - Error in - REGO_PowerRegulator_OnLoad - " + e.Message);
                }
            }
            // if on actual vessel should apply lastUpdateTime modifier before any processing happens
            // so that all Converters actually have EC buffer
            updateECCapacity();
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            node.AddValue("lastUpdateTime", lastUpdateTime);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            part.force_activate();
        }

        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();
            try
            {
                updateMultipliers();
                updateECCapacity();
            }
            catch (Exception e) {
                Debug.Log("[REGO] - Error in - REGO_PowerRegulator_OnFixedUpdate - " + e.ToString());
            }
        }

        // UGLY: copied from BaseConverter, maybe can be pulled into Utilities
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
                print("[REGO] - Error in - PowerRegulator_GetDeltaTime - " + e.Message);
                return 0;
            }
        }

        private void updateMultipliers()
        {
            var deltaTime = GetDeltaTime();
            if (deltaTime > 0)
            {
                currentMultiplier = (float)deltaTime;
            }
        }

        private void updateECCapacity()
        {
            try
            {
                double maxAmount = baseAmount * currentMultiplier;
                bool ecNodePresent = false;
                foreach (PartResource r in part.Resources)
                {
                    if (r.resourceName.Equals("ElectricCharge"))
                    {
                        ecNodePresent = true;
                        if (Math.Abs(r.maxAmount - maxAmount) > Regolith.Common.Utilities.FLOAT_TOLERANCE)
                        {
                            //Debug.Log("PowerRegulator: Updated EC capacity " + r.maxAmount + " to " + maxAmount + "(" + baseAmount + "*" + currentMultiplier + ")");
                            r.maxAmount = maxAmount;
                            if (r.amount > maxAmount)
                            {
                                r.amount = maxAmount;
                            }
                        }
                        break;
                    }
                }
                if (!ecNodePresent)
                {
                    ConfigNode newResourceNode = new ConfigNode("RESOURCE");
                    newResourceNode.AddValue("name", "ElectricCharge");
                    newResourceNode.AddValue("maxAmount", maxAmount);
                    newResourceNode.AddValue("amount", 0.0d);
                    part.AddResource(newResourceNode);
                    part.Resources.UpdateList();
                }
            }
            catch (Exception e)
            {
                Debug.Log("[REGO] - Error in - PowerRegulator_updateECCapacity - " + e.ToString());
            }
        }
    }
}
