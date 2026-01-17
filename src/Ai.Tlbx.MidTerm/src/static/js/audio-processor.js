// audio-processor.js
// AudioRecorderProcessor: Captures at 48kHz, downsamples to provider's required rate (16kHz/24kHz)

// Pre-calculated 2nd order Butterworth low-pass filter configs for supported target rates
// All assume 48kHz capture rate
const DOWNSAMPLE_CONFIGS = {
  // 48kHz -> 24kHz (ratio 2:1), LPF at 8kHz
  24000: {
    ratio: 2,
    b: [0.1550, 0.3101, 0.1550],
    a: [-0.6202, 0.2404]
  },
  // 48kHz -> 16kHz (ratio 3:1), LPF at 6kHz
  16000: {
    ratio: 3,
    b: [0.0976, 0.1953, 0.0976],
    a: [-0.9428, 0.3333]
  },
  // 48kHz -> 48kHz (no downsampling), LPF at 16kHz for consistency
  48000: {
    ratio: 1,
    b: [0.2929, 0.5858, 0.2929],
    a: [-0.0, 0.1716]
  }
};

class AudioRecorderProcessor extends AudioWorkletProcessor {
  constructor(options) {
    super();

    // Get target sample rate from processor options (default 24kHz for OpenAI)
    const targetRate = options?.processorOptions?.targetSampleRate || 24000;
    const config = DOWNSAMPLE_CONFIGS[targetRate];

    if (!config) {
      console.error(`[AudioProcessor] Unsupported target rate: ${targetRate}Hz. Using 24kHz.`);
      this.config = DOWNSAMPLE_CONFIGS[24000];
      this.targetRate = 24000;
    } else {
      this.config = config;
      this.targetRate = targetRate;
    }

    console.log(`[AudioProcessor] 48kHz -> ${this.targetRate}Hz (ratio ${this.config.ratio}:1)`);

    // Buffer size scales with target rate (base 2048 samples @ 24kHz ~ 85ms)
    const bufferSize = Math.floor(2048 * (this.targetRate / 24000));
    this.buffer = new Int16Array(bufferSize);
    this.bufferIndex = 0;
    this.isActive = true;
    this.isStopping = false;
    this.stopCountdown = 0;

    // Anti-aliasing filter state
    this.filterX = [0, 0];
    this.filterY = [0, 0];

    // Downsampling counter
    this.sampleIndex = 0;

    this.port.onmessage = (event) => {
      if (event.data?.command === 'stop') {
        console.log('[AudioProcessor] Received stop command');
        this.isStopping = true;
        this.stopCountdown = 3;
      }
    };
  }

  // 2nd order IIR Butterworth low-pass filter using config coefficients
  applyAntiAliasingFilter(sample) {
    const b = this.config.b;
    const a = this.config.a;

    const output = b[0] * sample
      + b[1] * this.filterX[0]
      + b[2] * this.filterX[1]
      - a[0] * this.filterY[0]
      - a[1] * this.filterY[1];

    // Shift history
    this.filterX[1] = this.filterX[0];
    this.filterX[0] = sample;
    this.filterY[1] = this.filterY[0];
    this.filterY[0] = output;

    return output;
  }

  process(inputs, outputs) {
    // Handle graceful shutdown
    if (this.isStopping) {
      if (this.stopCountdown > 0) {
        this.stopCountdown--;
        if (this.bufferIndex > 0 && this.stopCountdown === 0) {
          this.port.postMessage({
            audioData: this.buffer.slice(0, this.bufferIndex)
          });
          this.bufferIndex = 0;
        }
      } else {
        console.log('[AudioProcessor] Graceful shutdown complete');
        this.port.postMessage({ stopped: true });
        this.isActive = false;
        return false;
      }
    }

    const input = inputs[0]?.[0];
    if (!input || input.length === 0) {
      return true;
    }

    const ratio = this.config.ratio;

    // Process each sample: apply anti-aliasing filter, then downsample
    for (let i = 0; i < input.length; i++) {
      const filtered = this.applyAntiAliasingFilter(input[i]);

      // Downsample N:1 based on config ratio
      this.sampleIndex++;
      if (this.sampleIndex >= ratio) {
        this.sampleIndex = 0;

        // Convert filtered sample to PCM16
        const clamped = Math.max(-1, Math.min(1, filtered));
        const pcmValue = clamped < 0 ? clamped * 32768 : clamped * 32767;

        this.buffer[this.bufferIndex++] = Math.floor(pcmValue);

        // Send buffer when full
        if (this.bufferIndex >= this.buffer.length) {
          this.port.postMessage({
            audioData: this.buffer.slice(0)
          });
          this.bufferIndex = 0;
        }
      }
    }

    return true;
  }
}

registerProcessor('audio-recorder-processor', AudioRecorderProcessor);

// --- Playback Processor ---
// Receives 24kHz audio, upsamples to 48kHz with linear interpolation

const BUFFER_SIZE = 8640000; // ~180s (3 min) at 48kHz
const CROSSFADE_SAMPLES = 256;
const MIN_START_BUFFER = 9600; // ~200 ms @ 48 kHz

class PlaybackProcessor extends AudioWorkletProcessor {
  constructor(options) {
    super();
    this._buffer = new Float32Array(BUFFER_SIZE);
    this._writeIndex = 0;
    this._readIndex = 0;
    this._bufferFill = 0;
    this._isPlaying = false;
    this._isStopping = false;

    // Upsampling state
    this._lastInputSample = 0;

    // Crossfading state
    this._crossfadeBuffer = new Float32Array(CROSSFADE_SAMPLES).fill(0);
    this._isCrossfadingIn = false;
    this._crossfadeIndex = 0;

    // Audio enhancement state
    this._prevSample = 0;
    this._eqHistory = new Float32Array(4).fill(0);
    this._noiseGate = 0.002;
    this._enhancementEnabled = false;

    this.port.onmessage = (event) => {
      if (event.data.command === 'stop') {
        console.log('[PlaybackProcessor] Received stop command');
        this._isStopping = true;
      } else if (event.data.command === 'clear') {
        console.log('[PlaybackProcessor] Received clear command');
        this._readIndex = this._writeIndex;
        this._bufferFill = 0;
        this._isPlaying = false;
        this._isStopping = false;
        this._crossfadeIndex = 0;
        this._isCrossfadingIn = false;
        this._crossfadeBuffer.fill(0);
        this._lastInputSample = 0;
        this._buffer.fill(0);
      } else if (event.data.command === 'setEnhancement') {
        this._enhancementEnabled = !!event.data.enabled;
      } else if (event.data.audioData) {
        this._handleAudioData(event.data.audioData);
        if (!this._isPlaying && this._bufferFill >= MIN_START_BUFFER) {
          this._isPlaying = true;
        }
        this._isStopping = false;
      }
    };
  }

  _handleAudioData(audioData) {
    const inputData = audioData instanceof ArrayBuffer ? new Float32Array(audioData) : new Float32Array(audioData.buffer);

    // Upsample 24kHz -> 48kHz (2:1) with linear interpolation
    const upsampledLength = inputData.length * 2;

    if (this._bufferFill + upsampledLength > BUFFER_SIZE) {
      console.warn('[PlaybackProcessor] Buffer overflow, dropping new data.');
      return;
    }

    if (this._bufferFill === 0) {
      this._isCrossfadingIn = true;
      this._crossfadeIndex = 0;
      this._crossfadeBuffer.fill(0);
    }

    for (let i = 0; i < inputData.length; i++) {
      const curr = inputData[i];
      const prev = (i === 0) ? this._lastInputSample : inputData[i - 1];

      const interpolated = (prev + curr) * 0.5;

      this._buffer[this._writeIndex] = interpolated;
      this._writeIndex = (this._writeIndex + 1) % BUFFER_SIZE;

      this._buffer[this._writeIndex] = curr;
      this._writeIndex = (this._writeIndex + 1) % BUFFER_SIZE;
    }

    this._lastInputSample = inputData[inputData.length - 1];
    this._bufferFill += upsampledLength;
  }

  _applyCrossfade(sample, index, totalSamples, fadeIn, fadeOutBuffer) {
    const fadeOutGain = 1.0 - (index / totalSamples);
    const fadeInGain = index / totalSamples;
    return (fadeOutBuffer[index] * fadeOutGain) + (sample * fadeInGain);
  }

  _applyVoiceEQ(sample) {
    const alpha = 0.15;
    const boost = 1.1;
    const filtered = sample - this._eqHistory[0];
    this._eqHistory[0] = this._eqHistory[0] + alpha * filtered;
    return sample + (filtered * boost * 0.3);
  }

  _applyDeEmphasis(sample) {
    const alpha = 0.85;
    return sample * (1 - alpha) + this._prevSample * alpha;
  }

  _enhanceAudio(sample) {
    if (Math.abs(sample) < this._noiseGate) {
      sample = 0;
    }
    sample = this._applyVoiceEQ(sample);
    sample = this._applyDeEmphasis(sample);
    if (sample > 1.0) sample = 1.0;
    else if (sample < -1.0) sample = -1.0;
    return sample;
  }

  process(inputs, outputs, parameters) {
    const output = outputs[0];
    const channel = output[0];

    if (!channel || this._isStopping) {
      if (channel) channel.fill(0);
      return true;
    }

    if (!this._isPlaying) {
      if (this._bufferFill >= MIN_START_BUFFER) {
        this._isPlaying = true;
      } else {
        if (channel) channel.fill(0);
        return true;
      }
    }

    if (channel === undefined) {
      return true;
    }

    let generatedSamples = 0;
    for (let i = 0; i < channel.length; i++) {
      if (this._bufferFill > 0) {
        let sample = this._buffer[this._readIndex];

        if (this._isCrossfadingIn && this._crossfadeIndex < CROSSFADE_SAMPLES) {
          sample = this._applyCrossfade(sample, this._crossfadeIndex, CROSSFADE_SAMPLES, true, this._crossfadeBuffer);
          this._crossfadeIndex++;
        } else {
          this._isCrossfadingIn = false;
        }

        if (this._enhancementEnabled) {
          sample = this._enhanceAudio(sample);
        }

        channel[i] = sample;
        this._readIndex = (this._readIndex + 1) % BUFFER_SIZE;
        this._bufferFill--;
        generatedSamples++;
      } else {
        channel[i] = 0.0;
        if (this._bufferFill < MIN_START_BUFFER) {
          this._isPlaying = false;
        }
        if (generatedSamples > 0) {
          const start = (this._readIndex - Math.min(generatedSamples, CROSSFADE_SAMPLES) + BUFFER_SIZE) % BUFFER_SIZE;
          for (let j = 0; j < CROSSFADE_SAMPLES; j++) {
            this._crossfadeBuffer[j] = this._buffer[(start + j) % BUFFER_SIZE] ?? 0.0;
          }
        } else {
          this._crossfadeBuffer.fill(0);
        }
        continue;
      }
    }

    for (let i = generatedSamples; i < channel.length; i++) {
      channel[i] = 0.0;
    }

    if (this._isStopping && this._bufferFill === 0) {
      console.log('[PlaybackProcessor] Stopping after draining buffer.');
      this._isPlaying = false;
      return false;
    }

    return true;
  }
}

registerProcessor('playback-processor', PlaybackProcessor);
