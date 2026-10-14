using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CaptainPinkTurd.Core.Utilities
{
    [RequireComponent(typeof(AudioSource))]
    public class TypewriterText : MonoBehaviour
    {
        [Header("Typewriter Configs")]
        [SerializeField] private TMP_Text textUI;
        [SerializeField][Range(0.01f, 1f)] private float typingSpeed = 0.03f;
        
        [Header("Voice")]
        [SerializeField] private TypewriterAudioInfo defaultAudioInfo;
        [SerializeField] private TypewriterAudioInfo[] audioInfos;
        [SerializeField] private bool makePredictable;
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.28f;

        private TypewriterAudioInfo currentAudioInfo;
        private Dictionary<string, TypewriterAudioInfo> audioInfoDictionary;
        private AudioSource audioSource;
        private Coroutine typingCoroutine;
        private Action onTypingEnd;

        private bool isTyping;
        private bool playTypingAudio;
        
        public bool IsTyping => isTyping;
        
        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = voiceVolume;
            currentAudioInfo = defaultAudioInfo;
            InitializeAudioInfoDictionary();
        }
        private void OnDisable() => StopTyping();

        private void InitializeAudioInfoDictionary() 
        {
            audioInfoDictionary = new Dictionary<string, TypewriterAudioInfo>();
            if (defaultAudioInfo && !string.IsNullOrEmpty(defaultAudioInfo.speaker))
                audioInfoDictionary[defaultAudioInfo.speaker] = defaultAudioInfo;

            if (audioInfos == null) return;
            foreach (var audioInfo in audioInfos) 
            {
                if (audioInfo && !string.IsNullOrEmpty(audioInfo.speaker))
                    audioInfoDictionary[audioInfo.speaker] = audioInfo;
            }
            // The pink teenager is B in the memory, while Teen is the younger A.
            // The joined A&B figure uses B's lower tone in the boss conversation.
            if (audioInfoDictionary.TryGetValue("B", out var b))
            {
                audioInfoDictionary["TeenB"] = b;
                audioInfoDictionary["AB"] = b;
            }
        }
        public void SetTextUIActive(bool active) => textUI.gameObject.SetActive(active);
        public void StartTyping(string speaker, string line, TextAlignmentOptions textAlignment,
            Action onTypingEnd = null, bool playTypingAudio = true)
        {
            StopTyping();
            SetTextUIActive(true);
            this.onTypingEnd = onTypingEnd;
            this.playTypingAudio = playTypingAudio;

            textUI.alignment = textAlignment;
            typingCoroutine = StartCoroutine(TypeText(speaker, line ?? ""));
        }

        private IEnumerator TypeText(string speaker, string line)
        {
            List<DialogueToken> tokens = ParseLine(line, out string cleanText);

            textUI.text = cleanText;
            textUI.maxVisibleCharacters = 0;

            isTyping = true;

            float currentSpeed = typingSpeed;
            int visibleCharIndex = 0;
            int voicedCharIndex = 0;

            SetCurrentAudioInfo(speaker);
            foreach (var token in tokens)
            {
                switch (token.type)
                {
                    case TypewriterTokenType.Character:
                        // Count spoken letters, not punctuation/spacing or the characters of a TMP tag.
                        if (char.IsLetterOrDigit(token.character))
                        {
                            int frequency = currentAudioInfo && currentAudioInfo.soundFrequency != null
                                ? Mathf.Max(1, Mathf.CeilToInt(currentAudioInfo.soundFrequency.Evaluate(currentSpeed))) : 2;
                            if (playTypingAudio && voicedCharIndex % frequency == 0) PlayVoice(token.character);
                            voicedCharIndex++;
                        }
                        visibleCharIndex++;
                        textUI.maxVisibleCharacters = visibleCharIndex;

                        yield return new WaitForSecondsRealtime(currentSpeed);
                        break;

                    case TypewriterTokenType.Pause:
                        yield return new WaitForSecondsRealtime(token.value);
                        break;

                    case TypewriterTokenType.Speed:
                        currentSpeed = token.value;
                        break;
                }
            }

            isTyping = false;
            typingCoroutine = null;
            var completed = onTypingEnd;
            onTypingEnd = null;
            completed?.Invoke();
        }
        private List<DialogueToken> ParseLine(string line, out string cleanText)
        {
            List<DialogueToken> tokens = new List<DialogueToken>();
            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                // Handle TMP rich text <color=red>Hello</color>
                if (line[i] == '<')
                {
                    int end = line.IndexOf('>', i);
                    if (end != -1)
                    {
                        string richTag = line.Substring(i, end - i + 1);
                        builder.Append(richTag); // keep in display
                        i = end;
                        continue;
                    }
                }

                // Handle custom tags [pause=...] / [speed=...]
                if (line[i] == '[')
                {
                    int end = line.IndexOf(']', i);

                    // Missing closing bracket → treat as normal text
                    if (end == -1)
                    {
                        Debug.LogError($"Missing closing bracket for tag in line: {line}, treat it as visible text instead");
                        builder.Append(line[i]);
                        tokens.Add(new DialogueToken { type = TypewriterTokenType.Character, character = line[i] });
                        continue;
                    }

                    string tag = line.Substring(i + 1, end - i - 1);

                    if (TryParseTag(tag, out DialogueToken token))
                    {
                        tokens.Add(token);
                    }
                    else
                    {
                        // Invalid tag → treat as visible text
                        Debug.LogError($"Invalid tag '{tag}' in line: {line}, treat it as visible text instead");
                        for (int j = i; j <= end; j++)
                        {
                            builder.Append(line[j]);
                            tokens.Add(new DialogueToken
                            {
                                type = TypewriterTokenType.Character,
                                character = line[j]
                            });
                        }
                    }

                    i = end;
                    continue;
                }

                // Normal character
                builder.Append(line[i]);
                tokens.Add(new DialogueToken
                {
                    type = TypewriterTokenType.Character,
                    character = line[i]
                });
            }

            cleanText = builder.ToString();
            return tokens;
        }
        private bool TryParseTag(string tag, out DialogueToken token)
        {
            token = null;

            string[] parts = tag.Split('=');
            if (parts.Length != 2) return false;

            string key = parts[0];
            string valueStr = parts[1];
            float value;

            //[speed=default] will return typing speed to its default value in inspector
            if (valueStr is "default" or "Default" or "DEFAULT" && key is "speed" or "Speed" or "SPEED")
            {
                value = typingSpeed;
            }
            else if (!float.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return false;
            }

            switch (key)
            {
                case "pause":
                case "Pause":
                case "PAUSE":
                case "wait":
                case "Wait":
                case "WAIT":
                    token = new DialogueToken { type = TypewriterTokenType.Pause, value = value };
                    return true;

                case "speed":
                case "Speed":
                case "SPEED":
                    token = new DialogueToken { type = TypewriterTokenType.Speed, value = value };
                    return true;

                default:
                    return false;
            }
        }

        private void SetCurrentAudioInfo(string speaker) 
        {
            if (!string.IsNullOrEmpty(speaker) && audioInfoDictionary.TryGetValue(speaker, out var audioInfo))
            {
                currentAudioInfo = audioInfo;
            }
            else 
            {
                // Never let an unknown speaker inherit the previous character's voice.
                currentAudioInfo = defaultAudioInfo;
            }
        }
        private void PlayVoice(char currentCharacter)
        {
            if (!currentAudioInfo || currentAudioInfo.typingSoundClips == null ||
                currentAudioInfo.typingSoundClips.Length == 0 || audioSource.isPlaying) return;

            AudioClip soundClip = null;
            if (makePredictable)
            {
                //sound clip
                int hashCode = currentCharacter;
                int predictableIndex = hashCode % currentAudioInfo.typingSoundClips.Length;
                soundClip =  currentAudioInfo.typingSoundClips[predictableIndex];
                
                //pitch
                int minPitchInt = (int)(currentAudioInfo.minPitch * 100);
                int maxPitchInt = (int)(currentAudioInfo.maxPitch * 100);
                int pitchRangeInt = maxPitchInt - minPitchInt;
                if(pitchRangeInt != 0)
                {
                    int predictablePitchInt = (hashCode % pitchRangeInt) + minPitchInt;
                    float predictablePitch = predictablePitchInt / 100f;
                    audioSource.pitch = predictablePitch;
                }
                else
                {
                    audioSource.pitch = currentAudioInfo.minPitch;
                }
            }
            else
            {
                int randomIndex = Random.Range(0, currentAudioInfo.typingSoundClips.Length);
                soundClip = currentAudioInfo.typingSoundClips[randomIndex];
                
                audioSource.pitch = Random.Range(currentAudioInfo.minPitch, currentAudioInfo.maxPitch);
            }
            
            // Short clips have their own attack/release envelope. Let each blip finish instead of
            // restarting/cutting it at the next letter, which clicks and stacks voices at high speed.
            if (!soundClip) return;
            audioSource.clip = soundClip;
            audioSource.Play();
        }

        public void SkipTyping()
        {
            if (!isTyping) return;

            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            typingCoroutine = null;
            StopVoice();
            textUI.maxVisibleCharacters = textUI.text.Length;
            isTyping = false;
            var completed = onTypingEnd;
            onTypingEnd = null;
            completed?.Invoke();
        }

        /// <summary>Cancel a hidden/disabled panel without firing a stale line's completion callback.</summary>
        public void StopTyping()
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            typingCoroutine = null;
            isTyping = false;
            onTypingEnd = null;
            StopVoice();
        }

        private void StopVoice()
        {
            if (!audioSource) return;
            audioSource.Stop();
            audioSource.clip = null;
        }
    }
    public enum TypewriterTokenType
    {
        Character,
        Pause,
        Speed
    }
    
    public class DialogueToken
    {
        public TypewriterTokenType type;
        public char character;
        public float value;
    }
}
