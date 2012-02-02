﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NVorbis.Vorbis
{
	internal class Lpc
	{
		// en/decode lookups
		internal Drft fft = new Drft();

		internal int ln;
		internal int m;

		// Autocorrelation LPC coeff generation algorithm invented by
		// N. Levinson in 1947, modified by J. Durbin in 1959.

		// Input : n elements of time doamin data
		// Output: m lpc coefficients, excitation energy

		static internal float lpc_from_data(float[] data, float[] lpc, int n, int m)
		{
			float[] aut = new float[m + 1];
			float error;
			int i, j;

			// autocorrelation, p+1 lag coefficients

			j = m + 1;
			while (j-- != 0)
			{
				float d = 0;
				for (i = j; i < n; i++)
					d += data[i] * data[i - j];
				aut[j] = d;
			}

			// Generate lpc coefficients from autocorr values

			error = aut[0];
			/*
			if(error==0){
			  for(int k=0; k<m; k++) lpc[k]=0.0f;
			  return 0;
			}
			*/

			for (i = 0; i < m; i++)
			{
				float r = -aut[i + 1];

				if (error == 0)
				{
					for (int k = 0; k < m; k++)
						lpc[k] = 0.0f;
					return 0;
				}

				// Sum up this iteration's reflection coefficient; note that in
				// Vorbis we don't save it.  If anyone wants to recycle this code
				// and needs reflection coefficients, save the results of 'r' from
				// each iteration.

				for (j = 0; j < i; j++)
					r -= lpc[j] * aut[i - j];
				r /= error;

				// Update LPC coefficients and total error

				lpc[i] = r;
				for (j = 0; j < i / 2; j++)
				{
					float tmp = lpc[j];
					lpc[j] += r * lpc[i - 1 - j];
					lpc[i - 1 - j] += r * tmp;
				}
				if (i % 2 != 0)
					lpc[j] += lpc[j] * r;

				error *= 1.0f - r * r;
			}

			// we need the error value to know how big an impulse to hit the
			// filter with later

			return error;
		}

		// Input : n element envelope spectral curve
		// Output: m lpc coefficients, excitation energy

		internal float lpc_from_curve(float[] curve, float[] lpc)
		{
			int n = ln;
			float[] work = new float[n + n];
			float fscale = (float)(.5 / n);
			int i, j;

			// input is a real curve. make it complex-real
			// This mixes phase, but the LPC generation doesn't care.
			for (i = 0; i < n; i++)
			{
				work[i * 2] = curve[i] * fscale;
				work[i * 2 + 1] = 0;
			}
			work[n * 2 - 1] = curve[n - 1] * fscale;

			n *= 2;
			fft.backward(work);

			// The autocorrelation will not be circular.  Shift, else we lose
			// most of the power in the edges.

			for (i = 0, j = n / 2; i < n / 2; )
			{
				float temp = work[i];
				work[i++] = work[j];
				work[j++] = temp;
			}

			return (lpc_from_data(work, lpc, n, m));
		}

		internal void init(int mapped, int m)
		{
			ln = mapped;
			this.m = m;

			// we cheat decoding the LPC spectrum via FFTs
			fft.init(mapped * 2);
		}

		internal void clear()
		{
			fft.clear();
		}

		static internal float FAST_HYPOT(float a, float b)
		{
			return (float)Math.Sqrt((a) * (a) + (b) * (b));
		}

		// One can do this the long way by generating the transfer function in
		// the time domain and taking the forward FFT of the result.  The
		// results from direct calculation are cleaner and faster. 
		//
		// This version does a linear curve generation and then later
		// interpolates the log curve from the linear curve.

		internal void lpc_to_curve(float[] curve, float[] lpc, float amp)
		{

			for (int i = 0; i < ln * 2; i++)
				curve[i] = 0.0f;

			if (amp == 0)
				return;

			for (int i = 0; i < m; i++)
			{
				curve[i * 2 + 1] = lpc[i] / (4 * amp);
				curve[i * 2 + 2] = -lpc[i] / (4 * amp);
			}

			fft.backward(curve);

			{
				int l2 = ln * 2;
				float unit = (float)(1.0f / amp);
				curve[0] = (float)(1.0f / (curve[0] * 2 + unit));
				for (int i = 1; i < ln; i++)
				{
					float real = (curve[i] + curve[l2 - i]);
					float imag = (curve[i] - curve[l2 - i]);

					float a = real + unit;
					curve[i] = (float)(1.0 / FAST_HYPOT(a, imag));
				}
			}
		}
	}

}