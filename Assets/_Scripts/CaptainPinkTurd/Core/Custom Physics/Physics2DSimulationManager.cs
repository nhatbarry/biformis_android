using CaptainPinkTurd.Core.DesignPattern.Singleton;
using UnityEngine;

namespace CaptainPinkTurd.Core.CustomPhysics
{
    public class Physics2DSimulationManager : Singleton<Physics2DSimulationManager>
    {
        [SerializeField] private bool setScriptSimulationModeOnStart;

        protected override void Awake()
        {
            base.Awake();

            if (setScriptSimulationModeOnStart) Physics2D.simulationMode = SimulationMode2D.Script;
        }

        private void Update()
        {
            if(Physics2D.simulationMode != SimulationMode2D.Script) return;
            
            Physics2D.Simulate(Time.unscaledDeltaTime);
        }
    }
}