INCLUDE PensDown_Dialogue.ink
INCLUDE Test 1 Questions.ink
INCLUDE Teacher_Dialogue.ink

VAR currentQuestion = 1
VAR playerHp = 3 //default value, will be changed inside the player script

EXTERNAL ExamEndResult(result)
EXTERNAL OnAnswerChoose(result)

=== Main ===
{testBegin == false: -> Teacher_Countdown}
{currentQuestion: 
- 1: -> FirstQuestion
- 2: -> SecondQuestion
- 3: -> ThirdQuestion
} 


=== interactableTest ===
This is a square, nothing much to say.
-> END
