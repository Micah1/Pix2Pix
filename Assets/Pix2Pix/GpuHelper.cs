using UnityEngine;

namespace Pix2Pix
{
    static class GpuHelper
    {
        public enum ConvolutionMode { Forward, Backward }

        public static Tensor InvokeFunctionKernel(string name, Tensor input)
        {
            var compute = Pix2PixResources.Compute;
            var kernel = compute.FindKernel(name);

            uint tgn_x, tgn_y, tgn_z;
            compute.GetKernelThreadGroupSizes(kernel, out tgn_x, out tgn_y, out tgn_z);
            Debug.Assert(tgn_y == 1 && tgn_z == 1);

            var length = input.Data.Length;
            Debug.Assert(length % tgn_x == 0);

            var buffer_input  = new UnityEngine.ComputeBuffer(length, sizeof(float));
            var buffer_output = new UnityEngine.ComputeBuffer(length, sizeof(float));

            buffer_input.SetData(input.Data);
            compute.SetBuffer(kernel, "Input", buffer_input);
            compute.SetBuffer(kernel, "Output", buffer_output);
            compute.Dispatch(kernel, length / (int)tgn_x, 1, 1);

            var output = new Tensor(input.Shape);
            buffer_output.GetData(output.Data);

            buffer_input .Dispose();
            buffer_output.Dispose();

            return output;
        }

        public static Tensor InvokeNormalizationKernel(
            string name, Tensor input, Tensor scale, Tensor offset
        )
        {
            var compute = Pix2PixResources.Compute;
            var kernel = compute.FindKernel(name);

            uint tgn_x, tgn_y, tgn_z;
            compute.GetKernelThreadGroupSizes(kernel, out tgn_x, out tgn_y, out tgn_z);

            var length = input.Data.Length;
            var channels = input.Shape[2];

            Debug.Assert(channels % tgn_x == 0);
            Debug.Assert(channels == scale .Data.Length);
            Debug.Assert(channels == offset.Data.Length);

            var buffer_input  = new UnityEngine.ComputeBuffer(length,   sizeof(float));
            var buffer_scale  = new UnityEngine.ComputeBuffer(channels, sizeof(float));
            var buffer_offset = new UnityEngine.ComputeBuffer(channels, sizeof(float));
            var buffer_output = new UnityEngine.ComputeBuffer(length,   sizeof(float));

            buffer_input .SetData(input .Data);
            buffer_scale .SetData(scale .Data);
            buffer_offset.SetData(offset.Data);

            compute.SetInts( "InputShape", input.Shape);
            compute.SetInts("OutputShape", input.Shape);

            compute.SetBuffer(kernel, "Input" , buffer_input );
            compute.SetBuffer(kernel, "Filter", buffer_scale );
            compute.SetBuffer(kernel, "Bias"  , buffer_offset);
            compute.SetBuffer(kernel, "Output", buffer_output);

            compute.Dispatch(kernel, channels / (int)tgn_x, 1, 1);

            var output = new Tensor(input.Shape);
            buffer_output.GetData(output.Data);

            buffer_input .Dispose();
            buffer_scale .Dispose();
            buffer_offset.Dispose();
            buffer_output.Dispose();

            return output;
        }

        public static Tensor InvokeConvolutionKernel(
            ConvolutionMode mode, string name, Tensor input, Tensor filter, Tensor bias
        )
        {
            var compute = Pix2PixResources.Compute;
            var kernel = compute.FindKernel(name);

            uint tgn_x, tgn_y, tgn_z;
            compute.GetKernelThreadGroupSizes(kernel, out tgn_x, out tgn_y, out tgn_z);

            var deconv = (mode == ConvolutionMode.Backward);
            var outHeight = deconv ? input.Shape[0] * 2 : input.Shape[0] / 2;
            var outWidth  = deconv ? input.Shape[1] * 2 : input.Shape[1] / 2;
            var outChannels = filter.Shape[deconv ? 2 : 3];

            Debug.Assert(outHeight   % tgn_z == 0);
            Debug.Assert(outWidth    % tgn_y == 0);
            Debug.Assert(outChannels % tgn_x == 0);

            var output = new Tensor(new [] {outHeight, outWidth, outChannels});

            var buffer_input  = new UnityEngine.ComputeBuffer(input .Data.Length, sizeof(float));
            var buffer_filter = new UnityEngine.ComputeBuffer(filter.Data.Length, sizeof(float));
            var buffer_bias   = new UnityEngine.ComputeBuffer(bias  .Data.Length, sizeof(float));
            var buffer_output = new UnityEngine.ComputeBuffer(output.Data.Length, sizeof(float));

            buffer_input .SetData(input .Data);
            buffer_filter.SetData(filter.Data);
            buffer_bias  .SetData(bias  .Data);

            compute.SetInts( "InputShape", input .Shape);
            compute.SetInts("FilterShape", filter.Shape);
            compute.SetInts("OutputShape", output.Shape);

            compute.SetBuffer(kernel, "Input" , buffer_input );
            compute.SetBuffer(kernel, "Filter", buffer_filter);
            compute.SetBuffer(kernel, "Bias"  , buffer_bias  );
            compute.SetBuffer(kernel, "Output", buffer_output);

            compute.Dispatch(kernel,
                outChannels / (int)tgn_x,
                outWidth    / (int)tgn_y,
                outHeight   / (int)tgn_z
            );

            buffer_output.GetData(output.Data);

            buffer_input .Dispose();
            buffer_filter.Dispose();
            buffer_bias  .Dispose();
            buffer_output.Dispose();

            return output;
        }
    }
}
