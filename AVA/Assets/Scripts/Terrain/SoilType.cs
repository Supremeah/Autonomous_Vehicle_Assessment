﻿using UnityEngine;

/// <summary>
/// The terraintype defines the soil parameters to be used for this class instance.
/// </summary>
public enum TerrainType
{
    SandyBrendan,
    YoloLoamBrendan,
    Custom,
}
/// <summary>
/// The Soil Type Class defines all functions and constants needed to calculate the tyre-soil interaction. 
/// A set of soil types are defined (can also be manually added), which combined with vehicle data can be used for calculating the acting forces and sinkage.
/// </summary>
public class SoilType
{
    public string terrainName;
    private TerrainType terrainType;  // Teerrain Type Name

    //// OBSOLETE ///Bekker Parameters 
    //private float n;        // Pressure-Sinkage Parameter []
    //private float k_c;      // Pressure-Sinkage Parameter [kN/m^(n+1)]
    //private float k_phi;    // Pressure-Sinkage Parameter [kN/m^(n+2)]

    //private float k_o;      // Unloading/Reloading Terrain Parameter [kN/m^3]
    //private float A_u;      // Unloading/Reloading Terrain Parameter [kN/m^4]

    private float k_1;      //[]
    private float k_2;      //[]
    private float n;        //[]
    private float c;        //Cohesion [Pa]
    private float phi;      //Angle of Shear Resistance [rad]
    private float Kx;       //[m]
    private float Ky;       //[m]
    private float gamma_s;  //Density [kg/m^3]
    private const float DeltaT_desired = Mathf.Deg2Rad * 10f;//0.01f; //[rad]
    private const float delta = 0.1f;
    private const int kmax = 100;
    private const float tol = 0.01f;

    /// <summary>
    /// The soil type contains all relevant soil parameters to be used in the calculations for this class instance.
    /// </summary>
    /// <param name="_terrainType"></param>
    public SoilType(TerrainType _terrainType)
    {
        terrainType = _terrainType;
        switch (_terrainType)
        {
            case TerrainType.SandyBrendan:
                terrainName = _terrainType.ToString();
                k_1 = 2f;
                k_2 = 17659.75f;
                n = 0.77f;
                c = 130f;
                phi = 31.1f * Mathf.Deg2Rad;
                Kx = 0.038f;
                Ky = 0.038f;
                gamma_s = 1600f;
                break;
            case TerrainType.YoloLoamBrendan:
                terrainName = _terrainType.ToString();
                k_1 = 3.25f;
                k_2 = 4600f;
                n = 0.99f;
                c = 22670f;
                phi = 22f * Mathf.Deg2Rad;
                Kx = 0.015f;
                Ky = 0.015f;
                gamma_s = 1258f;
                break;
        }
    }
    /// <summary>
    /// Custom soil type creator.
    /// </summary>
    /// <param name="_terrainType">Name of terrain</param>
    /// <param name="_k1">Reece [Unitless]</param>
    /// <param name="_k2">Reece [Unitless]</param>
    /// <param name="_n">Reece [Unitless]</param>
    /// <param name="_c">Cohesion [N/m^2] / [Pa]</param>
    /// <param name="_phi">Angle of shearing resistance [rad]</param>
    /// <param name="_KxKy">Shear deformation parameter [m]</param>
    /// <param name="_gamma_s">Density [kg/m^3]</param>
    public SoilType(string _terrainType, float _k1, float _k2, float _n, float _c, float _phi, float _KxKy, float _gamma_s)
    {
        terrainType = TerrainType.Custom;
        terrainName = _terrainType;
        k_1 = _k1;
        k_2 = _k2;
        n = _n;
        c = _c;
        phi = _phi * Mathf.Deg2Rad;
        Kx = _KxKy;
        Ky = _KxKy;
        gamma_s = _gamma_s;
    }
    /// <summary>
    /// Sums up the total vertical stress from the normal and shear stress components based on the entry angle, with a fixed output angle. (12.5) Wong 2010
    /// </summary>
    /// <param name="theta_e">Entry Angle</param>
    /// <param name="tyreWidth"></param>
    /// <param name="tyreRadius"></param>
    /// <param name="pressurePlateMainDimension"></param>
    /// <param name="slipRatio"></param>
    /// <returns>W [N]</returns>
    public float VerticalStress(float theta_e, float tyreWidth, float tyreRadius, float pressurePlateMainDimension, float slipRatio)
    {
        float sum = 0;
        float theta_tot = theta_e - ExitAngle();
        int Steps = (int)(theta_tot / DeltaT_desired);
        float DeltaT = theta_tot / Steps;
        for (int i = 0; i < Steps+1; i++)
        {
            float theta = ExitAngle() + DeltaT * i;
            float normalPressure = RadialStress(theta, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
            Debug.Log($"Normal Stress at {theta}[rad] = {normalPressure}[Pa]");
            float shearStress = ShearStress(theta, normalPressure,  tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
            Debug.Log($"Shear Stress at {theta}[rad] = {shearStress}[Pa]");
            if (theta < 0)  // Rear part, theta2 (exit) -> 0
            {
                sum -= (normalPressure * Mathf.Cos(theta) - shearStress * Mathf.Sin(theta)) * DeltaT;
            }
            else            // Front part, 0 -> theta1 (entry)
            {
                sum += (normalPressure * Mathf.Cos(theta) + shearStress * Mathf.Sin(theta)) * DeltaT;
            }
        }
        return (tyreWidth * tyreRadius) / 2 * sum;
    }
    /// <summary>
    /// Calculates the external motion resistance of the tyre from the horizontal normal pressure component. (12.6) Wong 2010
    /// </summary>
    /// <param name="theta_e">Tyre Entry Angle</param>
    /// <param name="tyreWidth"></param>
    /// <param name="tyreRadius"></param>
    /// <param name="pressurePlateMainDimension"></param>
    /// <param name="slipRatio"></param>
    /// <returns>Motion resistance R_t [N]</returns>
    public float MotionResistance(float theta_e, float tyreWidth, float tyreRadius, float pressurePlateMainDimension, float slipRatio)
    {
        float sum = 0;
        float theta_tot = theta_e - ExitAngle();
        int Steps = (int)(theta_tot / DeltaT_desired + 1);
        float DeltaT = theta_tot / (Steps - 1);

        for (int i = 0; i < Steps; i++)
        {
            float theta = ExitAngle() + DeltaT * i;
            float normalPressure = RadialStress(theta, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
            if (theta > 0)  // Front part, 0 -> theta1 (entry)
            {
                sum += normalPressure * Mathf.Sin(theta) * DeltaT;
            }
            else            // Rear part, theta2 (exit) -> 0
            {
                sum += normalPressure * Mathf.Sin(theta) * DeltaT; // - - = +
            }
        }
        return (tyreWidth * tyreRadius) / 2 * sum;
    }
    /// <summary>
    /// Calculates the thrust of the tyre from the horizontal shear stress component over the tyre. (12.7) Wong 2010
    /// </summary>
    /// <param name="theta_e">Tyre Entry Angle</param>
    /// <param name="tyreWidth"></param>
    /// <param name="tyreRadius"></param>
    /// <param name="pressurePlateMainDimension"></param>
    /// <param name="slipRatio"></param>
    /// <returns>Thrust F [N]</returns>
    public float Thrust(float theta_e, float tyreWidth, float tyreRadius, float pressurePlateMainDimension, float slipRatio)
    {
        float sum = 0;
        float theta_tot = theta_e - ExitAngle();
        int Steps = (int)(theta_tot / DeltaT_desired + 1);
        float DeltaT = theta_tot / (Steps - 1);

        for (int i = 0; i < Steps; i++)
        {
            float theta = ExitAngle() + DeltaT * i;
            float normalPressure = RadialStress(theta, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
            float shearStress = ShearStress(theta, normalPressure, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
            if (theta > 0)  // Front part, 0 -> theta1 (entry)
            {
                sum += shearStress * Mathf.Cos(theta) * DeltaT;
            }
            else            // Rear part, theta2 (exit) -> 0
            {
                sum -= shearStress * Mathf.Cos(theta) * DeltaT;
            }
        }
        return (tyreWidth * tyreRadius) / 2 * sum;
    }
    /// <summary>
    /// Calculates the applied torque to a driven tyre from the soil shear stress. (12.9) Wong 2010
    /// </summary>
    /// <param name="theta_e">Tyre Entry Angle</param>
    /// <param name="tyreWidth"></param>
    /// <param name="tyreRadius"></param>
    /// <param name="pressurePlateMainDimension"></param>
    /// <param name="slipRatio"></param>
    /// <returns>Torque M [N*m]</returns>
    public float Torque(float theta_e, float tyreWidth, float tyreRadius, float pressurePlateMainDimension, float slipRatio)
    {
        float sum = 0;
        float theta_tot = theta_e - ExitAngle();
        int Steps = (int)(theta_tot / DeltaT_desired + 1);
        float DeltaT = theta_tot / (Steps - 1);

        for (int i = 0; i < Steps; i++)
        {
            float theta = ExitAngle() + DeltaT * i;
            float normalPressure = RadialStress(theta, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
            float shearStress = ShearStress(theta, normalPressure, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
            if (theta > 0)  // Front part, 0 -> theta1 (entry)
            {
                sum += shearStress * DeltaT;
            }
            else            // Rear part, theta2 (exit) -> 0
            {
                sum -= shearStress * DeltaT;
            }
        }
        return (tyreWidth * tyreRadius * tyreRadius) / 4 * sum;
    }

    private float LineSearch(float tyreWidth, float tyreRadius, float pressurePlateMainDimension, float slipRatio)
    {
        int n = 0;
        float step = delta;
        while (VerticalStress(0f, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio) < VerticalStress(step, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio) && n < kmax)
        {
            step /= 2f;
            n++;
        }


        /// Phase 1 - Golden Section Search ///
        // Golden section search values for first iteration (0 as lower bracket)
        int q = 0;  // Golden section search exponent
        float alpha_l = 0;
        float alpha_i = Alpha(q, step);
        float alpha_u = Alpha(q + 1, step);

        // Golden section loop for bracketing of the optimum point
        n = 1;
        float VerticalStress0 = VerticalStress(Alpha(q, step), tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
        float VerticalStress1 = VerticalStress(Alpha(q + 1, step), tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
        while (n < kmax && VerticalStress0 > VerticalStress1)
        {
            q++;
            alpha_l = Alpha(q - 1, step);
            alpha_i = Alpha(q, step);
            alpha_u = Alpha(q + 1, step);
            n++;
        }

        // If flat optimum, make one more iteration
        VerticalStress0 = VerticalStress(Alpha(q, step), tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
        VerticalStress1 = VerticalStress(Alpha(q + 1, step), tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
        if (VerticalStress0 == VerticalStress1)
        {
            q++;
            alpha_l = Alpha(q - 1, step);
            alpha_i = Alpha(q, step);
            alpha_u = Alpha(q + 1, step);
        }

        if (n == kmax)
        {
            Debug.Log("No Brackets found in Linesearch");
        }

        /// Phase 2 - Polynomial Interpolation ///
        n = 1;

        // Check initial polynomial point
        // Calculates top point of interpolated polynomial based on a1 and a2
        float alpha_bar = -A1(alpha_l, alpha_i, alpha_u, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio)
                    / (2 * A2(alpha_l, alpha_i, alpha_u, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio));

        // Deviation between top point and intermediate point
        float dev = Mathf.Abs(alpha_bar - alpha_i);

        if (VerticalStress(alpha_l, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio) == VerticalStress(alpha_u, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio))
        {
            alpha_bar = alpha_l; // Returns first minimum point
        }
        else
        {
            while (n < kmax && dev > tol)
            {
                n++;
                alpha_bar = -A1(alpha_l, alpha_i, alpha_u, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio)
                / (2 * A2(alpha_l, alpha_i, alpha_u, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio));
                dev = Mathf.Abs(alpha_bar - alpha_i);

                if (alpha_i < alpha_bar)   // Step 4
                {
                    if (VerticalStress(alpha_i, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio) < VerticalStress(alpha_bar, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio))   // Step 4a
                    {
                        alpha_u = alpha_bar;
                    }
                    else        // Step 4b
                    {
                        alpha_l = alpha_i;
                        alpha_i = alpha_bar;
                    }
                }
                else    // Step 5
                {
                    if (VerticalStress(alpha_i, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio) < VerticalStress(alpha_bar, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio))   // Step 5a
                    {
                        alpha_l = alpha_bar;
                    }
                    else        // Step 5b
                    {
                        alpha_u = alpha_i;
                        alpha_i = alpha_bar;
                    }
                }
            }
        }

        float finalStepSize = alpha_bar;


        float theta_e = 20f * Mathf.Deg2Rad;
        return theta_e;
    }

    private float Alpha(float q, float delta)
    {
        float alpha_q = delta;
        for (int i = 0; i < q; i++)
        {
            alpha_q += delta * Mathf.Pow((1 + Mathf.Sqrt(5)) / 2f, i);
        }
        return alpha_q;
    }

    private float A1(float alpha_l, float alpha_i, float alpha_u, float tyreWidth, float tyreRadius, float pressurePlateMainDimension, float slipRatio)
    {
        float f_l = VerticalStress(alpha_l, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
        float f_i = VerticalStress(alpha_i, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
        float f_u = VerticalStress(alpha_u, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
        return (f_i - f_l) / (alpha_i - alpha_l)
            - A2(alpha_l, alpha_i, alpha_u, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio)
            * (alpha_l + alpha_i);
    }

    private float A2(float alpha_l, float alpha_i, float alpha_u, float tyreWidth, float tyreRadius, float pressurePlateMainDimension, float slipRatio)
    {
        float f_l = VerticalStress(alpha_l, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
        float f_i = VerticalStress(alpha_i, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
        float f_u = VerticalStress(alpha_u, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
        return 1 / (alpha_u - alpha_i) * ((f_u - f_l) / (alpha_u - alpha_l) - (f_i - f_l) / (alpha_i - alpha_l));
    }




    /// <summary>
    /// Determines the tyre Entry Angle based on the vertical force equilibrium of (12.5) Wong 2010 with -5deg exit angle.
    /// </summary>
    /// <param name="tyreWidth"></param>
    /// <param name="tyreRadius"></param>
    /// <param name="pressurePlateMainDimension"></param>
    /// <param name="slipRatio"></param>
    /// <returns>theta1 [rad]</returns>
    public float EntryAngle(float tyreWidth, float tyreRadius, float pressurePlateMainDimension, float slipRatio)
    {
        // Insert fminsearch here, currently fixed.
        //return Mathf.PI / 6;

        //float theta_e = LineSearch(tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
        float theta_e = Mathf.PI / 4f;
        //float theta_e = 20f * Mathf.Deg2Rad;// FindMinimum();
        return theta_e;
    }
    /// <summary>
    /// Fixed Exit angle based on Brendan Juin-Yih Chan 2008 Page 82
    /// </summary>
    /// <returns>theta2 [rad]</returns>
    public float ExitAngle()
    {
        // Fixed according to Brendan
        return -5f * Mathf.Deg2Rad;
    }
    /// <summary>
    /// Calculates the shear displacement along the contact interface from front to rear based on the slipratio. (4.9) Chan 2008
    /// </summary>
    /// <param name="angle">Position along contact interface to evaluate the shear displacement</param>
    /// <param name="tyreWidth"></param>
    /// <param name="tyreRadius"></param>
    /// <param name="pressurePlateMainDimension"></param>
    /// <param name="slipRatio"></param>
    /// <returns>j_x [m]</returns>
    public float ShearDisplacement(float angle, float tyreWidth, float tyreRadius, float pressurePlateMainDimension, float slipRatio)
    {
        float entryAngle = EntryAngle(tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
        return tyreRadius * ((entryAngle - angle) - (1f - slipRatio) * (Mathf.Sin(entryAngle) - Mathf.Sin(angle)));
    }
    /// <summary>
    /// Calculates the maximum shear stress for a given soil and applied normal stress. (5.16) Wong 2010
    /// </summary>
    /// <param name="normalPressure">Applied normal pressure at current point.</param>
    /// <returns>s_max [N/m^2] or [Pa]</returns>
    public float MaxShear(float normalPressure)
    {
        return c + normalPressure * Mathf.Tan(phi);
    }
    /// <summary>
    /// Calculates the shear stress along the contact interface from Janosi and Hanamoto. (5.2) Wong 2010
    /// </summary>
    /// <param name="angle">Angle corresponding the position to evalute the shear stress at. [rad]</param>
    /// <param name="normalPressure">Normal pressure at specific point along contact interface. [N/m^2] or [Pa]</param>
    /// <param name="tyreWidth"></param>
    /// <param name="tyreRadius"></param>
    /// <param name="pressurePlateMainDimension"></param>
    /// <param name="slipRatio"></param>
    /// <returns>S [Pa] or [N/m^2]</returns>
    public float ShearStress(float angle, float normalPressure, float tyreWidth, float tyreRadius, float pressurePlateMainDimension, float slipRatio)
    {
        float shearDisplacement = ShearDisplacement(angle, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
        Debug.Log($"Shear Displacement at {angle}[rad] = {shearDisplacement}[m]");
        return MaxShear(normalPressure) * (1f - Mathf.Exp(- shearDisplacement / Kx));
    }
    /// <summary>
    /// Calculates the angle corresponding with the highest radial stress along the contact interface, where the radial functions switch from (4.14) to (4.15). 
    /// The angle is limited to be within 0 and 1/3 * phi. (4.17) Chan 2008
    /// </summary>
    /// <param name="slipRatio"></param>
    /// <returns>thetaN [rad]</returns>
    public float MaxRadialStressAngle(float slipRatio)
    {
        float k = Mathf.Tan(Mathf.PI / 4f - phi / 2f); // kappa/my
        float y = -(Mathf.Pow(k, 2f) - Mathf.Sqrt(Mathf.Pow(k, 4f) * Mathf.Pow(slipRatio, 2f) - 2 * Mathf.Pow(k, 4f) 
            * slipRatio + Mathf.Pow(k, 4f) + Mathf.Pow(k, 2f) * Mathf.Pow(slipRatio, 2) - 2 * Mathf.Pow(k, 2f) 
            * slipRatio)) / (k * (slipRatio - 1) * (Mathf.Pow(k, 2f) + 1));
        float x = -(Mathf.Sqrt(Mathf.Pow(k,4f) * Mathf.Pow(slipRatio,2) - 2 * Mathf.Pow(k,4f) * slipRatio 
            + Mathf.Pow(k,4f) + Mathf.Pow(k,2f) * Mathf.Pow(slipRatio, 2) - 2 * Mathf.Pow(k,2f) * slipRatio) + 1) 
            / ((slipRatio - 1) * (Mathf.Pow(k,2) + 1));
        float angle = Mathf.Abs(Mathf.Atan2(Mathf.Abs(y), Mathf.Abs(x)));
        return Mathf.Min(Mathf.Max(angle,0f),(1f / 3f) * phi);
    }
    /// <summary>
    /// Calculates the Radial Stress for the front part of the contact interface. (4.14) Chan 2008
    /// </summary>
    /// <param name="angle">Angle corresponding to the point where the radial stress is to be evaluated at [rad]</param>
    /// <param name="tyreWidth"></param>
    /// <param name="tyreRadius"></param>
    /// <param name="pressurePlateMainDimension"></param>
    /// <param name="slipRatio"></param>
    /// <returns>sigma_nf [Pa] or [N/m^2]</returns>
    public float RadialStressFront(float angle, float tyreWidth, float tyreRadius, float pressurePlateMainDimension, float slipRatio)
    {
        float entryAngle = EntryAngle(tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
        return (c * k_1 + gamma_s * pressurePlateMainDimension * k_2) * Mathf.Pow(tyreRadius / pressurePlateMainDimension, n) 
            * Mathf.Pow(Mathf.Cos(angle) - Mathf.Cos(entryAngle), n);
    }
    /// <summary>
    /// Calculates the Radial Stress for the rear part of the contact interface. (4.15) Chan 2008
    /// </summary>
    /// <param name="angle">Angle corresponding to the point where the radial stress is to be evaluated at [rad]</param>
    /// <param name="tyreWidth"></param>
    /// <param name="tyreRadius"></param>
    /// <param name="pressurePlateMainDimension"></param>
    /// <param name="slipRatio"></param>
    /// <returns>sigma_nr [Pa] or [N/m^2]</returns>
    private float RadialStressRear(float angle, float tyreWidth, float tyreRadius, float pressurePlateMainDimension, float slipRatio)
    {
        float entryAngle = EntryAngle(tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
        float sigma_nr = (c * k_1 + gamma_s * pressurePlateMainDimension * k_2) 
            * Mathf.Pow(tyreRadius / pressurePlateMainDimension, n) 
            * Mathf.Pow(Mathf.Cos(entryAngle - (angle - ExitAngle()) / (MaxRadialStressAngle(slipRatio) - ExitAngle()) 
            * (entryAngle - MaxRadialStressAngle(slipRatio))) - Mathf.Cos(entryAngle), n);

        return sigma_nr;
    }
    /// <summary>
    /// Assembles both radial stress functions sigma_nf (4.14) and sigma_nr (4.15) from Chan 2008 based on max stress turning angle, thetaN.
    /// </summary>
    /// <param name="angle">Input angle where stress is evaluated [rad]</param>
    /// <param name="tyreWidth"></param>
    /// <param name="tyreRadius"></param>
    /// <param name="pressurePlateMainDimension"></param>
    /// <param name="slipRatio"></param>
    /// <returns>sigma_n [Pa] or [N/m^2]</returns>
    public float RadialStress(float angle, float tyreWidth, float tyreRadius, float pressurePlateMainDimension, float slipRatio)
    {
        float entryAngle = EntryAngle(tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
        float sigma_n = 0f;
        if (angle <= entryAngle && angle >= ExitAngle())
        {
            //Debug.Log("Inside angles!!!");
            if (angle >= MaxRadialStressAngle(slipRatio))
            {
                //Debug.Log("In front of maximum");
                sigma_n = RadialStressFront(angle, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
            }
            else
            {
                //Debug.Log("Behind maximum");
                sigma_n = RadialStressRear(angle, tyreWidth, tyreRadius, pressurePlateMainDimension, slipRatio);
            }
        }
        return sigma_n;
    }






    //public float BekkerPressureSinkage(float sinkage, float pressurePlateDimension)
    //{
    //    return (k_c / pressurePlateDimension + k_phi) * Mathf.Pow(sinkage, n);
    //}

    //public float BekkerUnloading(float sinkage, float unloadingSinkage, float unloadingPressure)
    //{
    //    float k_u = k_o + A_u * unloadingSinkage;
    //    return unloadingPressure - k_u * (unloadingSinkage - sinkage);
    //}

    //public float ReecePressureSinkage(float sinkage, float pressurePlateDimension)
    //{
    //    return (c * k_1 + gamma_s * pressurePlateDimension * k_2) * Mathf.Pow((sinkage / pressurePlateDimension), n);
    //}
}
