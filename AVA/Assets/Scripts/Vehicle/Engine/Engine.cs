﻿using UnityEngine;

public class Engine
{
    // ----- Curves ----- // 
    private AnimationCurve TorqueCurve;
    private AnimationCurve PowerCurve;
    private AnimationCurve FrictionCurve;

    // ----- Engine ----- // 
    public int MinRpm, MaxRpm, StallRpm;        // Engine maximum/minimum RPM
    public int MaxTorqueRpm, MaxPowerRpm;       // RPM at maximum Torque and Power
    private float maxTorque;
    public float Rpm;                           // Current engine status
    private float Inertia;                      // Mass moment of inertia [kg*m^2]
    private float AngularAccel;                 // Engine Angular acceleration 

    /// <summary>
    /// Instantiate an engine model from torque and power curves.
    /// </summary>
    /// <param name="torqueCurve">Engine torque curve (Torque/RPM)</param>
    /// <param name="powerCurve">Engine torque curve (Power/RPM)</param>
    /// <param name="frictionCurve">Engine friction curve (Nm/RPM)</param>
    /// <param name="inertia">Engine mass moment of inertia [kg m^3]</param>
    public Engine(AnimationCurve torqueCurve, AnimationCurve powerCurve, AnimationCurve frictionCurve, float inertia)
    {
        TorqueCurve = torqueCurve;
        PowerCurve = powerCurve;
        FrictionCurve = frictionCurve;
        Inertia = inertia;

        MinRpm = (int)torqueCurve.keys[0].time;
        MaxRpm = (int)torqueCurve.keys[torqueCurve.length - 1].time;

        // Extract MaxTorqueRpm from TorqueCurve
        maxTorque = 0;
        foreach (Keyframe keyframe in torqueCurve.keys)
        {
            if (keyframe.value > maxTorque)
            {
                maxTorque = (int)keyframe.value;
                MaxTorqueRpm = (int)keyframe.time;
            }
        }

        // Extract MaxPowerRpm from PowerCurve
        int MaxPower = 0;
        foreach (Keyframe keyframe in powerCurve.keys)
        {
            if (keyframe.value > MaxPower)
            {
                MaxPower = (int)keyframe.value;
                MaxPowerRpm = (int)keyframe.time;
            }
        }
    }

    /// <summary>
    /// Calculates engine torque from torque/Rpm curve based on the current RPM.
    /// </summary>
    /// <param name="throttle">Current engine throttle opening (0-100%).</param>
    /// <returns>Current engine torque [Nm].</returns>
    public float Torque(float throttle)
    {
        float friction = FrictionCurve.Evaluate(Rpm);
        return (TorqueCurve.Evaluate(Rpm) + friction) * throttle - friction;
    }

    /// <summary>
    /// Calculates engine power from Power/Rpm curve based on the current RPM.
    /// </summary>
    /// <returns>Current engine power.</returns>
    public float Power()
    {
        return PowerCurve.Evaluate(Rpm);
    }

    /// <summary>
    /// Updates the engine RPM based on clutch and accelerator. When clutch is fully engaged transmission rpm = engine rpm.
    /// </summary>
    /// <param name="data">Vehicle databus</param>
    public int[][] Update(int[][] data)
    {
        // Engine up and running
        if (data[Channel.Vehicle][VehicleData.EngineWorking] == 1)
        {
            float throttle = data[Channel.Input][InputData.Throttle] / 10000.0f;
            Rpm = data[Channel.Vehicle][VehicleData.EngineRpm] / 1000.0f;
            Rpm += AngularAccel * Time.deltaTime;
            Rpm = Mathf.Max(Rpm, StallRpm); // Limits Rpm to not go below stall Rpm

            if(Rpm < StallRpm)
            {
                Rpm = 0;
                data[Channel.Vehicle][VehicleData.EngineStalled] = 1;
                data[Channel.Vehicle][VehicleData.EngineWorking] = 0;
            }

            data[Channel.Vehicle][VehicleData.EngineTorque] = (int)(Torque(throttle) * 1000.0f);
            data[Channel.Vehicle][VehicleData.EnginePower] = (int)(Power() * throttle * 1000.0f);
            data[Channel.Vehicle][VehicleData.EngineLoad] = (int)(Torque(throttle) / maxTorque * 1000.0f);
            data[Channel.Vehicle][VehicleData.EngineRpm] = (int)(Rpm * 1000.0f);
        }

        return data;
    }

    public void Equilibrium(int[][] data)
    {
        float EngineTorque = data[Channel.Vehicle][VehicleData.EngineTorque] / 1000.0f;
        float ClutchSlipTorque = data[Channel.Vehicle][VehicleData.ClutchSlipTorque] / 1000.0f;
              
        AngularAccel = (EngineTorque - ClutchSlipTorque) / Inertia;
    }
}

