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

        private TypewriterAudioInfo currentAudioInfo;
        private Dictionary<string, TypewriterAudioInfo> audioInfoDictionary;
        private AudioSource audioSource;
        private Coroutine typingCoroutine;
        private Action onTypingEnd;

        private bool isTyping;
        private string currentLine;
        
        public bool IsTyping => isTyping;
        
        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            currentAudioInfo = defaultAudioInfo;
            InitializeAudioInfoDictionary();
        }
        private void InitializeAudioInfoDictionary() 
        {
            audioInfoDictionary = new Dictionary<string, TypewriterAudioInfo>
            {
                { defaultAudioInfo.speaker, defaultAudioInfo }
            };

            if (audioInfos == null || audioInfoDictionary.Count == 0) return;
            foreach (var audioInfo in audioInfos) 
            {
                audioInfoDictionary.Add(audioInfo.speaker, audioInfo);
            }
        }
        public void SetTextUIActive(bool active) => textUI.gameObject.SetActive(active);
        public void StartTyping(string speaker, string line, TextAlignmentOptions textAlignment, Action onTypingEnd = null)
        {
            SetTextUIActive(true);
            currentLine = line;

            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
            }
            this.onTypingEnd = onTypingEnd;

            textUI.alignment = textAlignment;
            typingCoroutine = StartCoroutine(TypeText(speaker, line));
        }

        private IEnumerator TypeText(string speaker, string line)
        {
            List<DialogueToken> tokens = ParseLine(line, out string cleanText);

            textUI.text = cleanText;
            textUI.maxVisibleCharacters = 0;

            isTyping = true;

            float currentSpeed = typingSpeed;
            int visibleCharIndex = 0;

            SetCurrentAudioInfo(speaker);
            foreach (var token in tokens)
            {
                switch (token.type)
                {
                    case TypewriterTokenType.Character:
                        if (visibleCharIndex % Mathf.CeilToInt(currentAudioInfo.soundFrequency.Evaluate(currentSpeed)) == 0 &&
                            !char.IsWhiteSpace(token.character))
                        {
                            PlayVoice(textUI.text[visibleCharIndex]);
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

            onTypingEnd?.Invoke();
            isTyping = false;
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
            if (audioInfoDictionary.TryGetValue(speaker, out var audioInfo)) 
            {
                currentAudioInfo = audioInfo;
            }
            else 
            {
                Debug.LogWarning("Failed to find audio info for speaker: " + speaker);
            }
        }
        private void PlayVoice(char currentCharacter)
        {
            if (currentAudioInfo.stopAudioSource)
            {
                audioSource.Stop();
            }

            AudioClip soundClip = null;
            if (makePredictable)
            {
                //sound clip
                int hashCode = currentCharacter.GetHashCode();
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
            
            audioSource.PlayOneShot(soundClip);
        }

        public void SkipTyping()
        {
            if (!isTyping) return;

            StopCoroutine(typingCoroutine);
            textUI.maxVisibleCharacters = textUI.text.Length;
            onTypingEnd?.Invoke();
            isTyping = false;
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