EXTERNAL OnTestBegin()
EXTERNAL OnAnswerFound()

VAR testBegin = false

// Pens Down Dialogue
=== FoundAnswer ===
{currentQuestion:
- 1: ~ foundAnswer1 = true
- 2: ~ foundAnswer2 = true
- 3: ~ foundAnswer3 = true
}
~ OnAnswerFound()
->-> 

=== CheckOtherStudent(hasAnswer) ===
{hasAnswer: -> HasAnswer|-> NoAnswer}

= NoAnswer 
// Checking over other Students work (Doesn't have the answer)
Ahh. #speaker:Player
This answer doesn't seem right 
-> DONE

= HasAnswer
// Checking over other Students work (Has the answer)
Oh? #speaker:Player
-> FoundAnswer ->
The answer to the question, yeah that seems right! Maybe I should copy this answer 
-> DONE

=== introduction_to_watch ===
// Played when player stops time for the first time
Wait. Did time just stop? #speaker:Player
That's means I can...
-> DONE


=== poi_classroom_no_answer ===
// Checking out Point of Interest Classroom (Doesn't have the answer)
Interesting... #speaker:Player
But the answer doesn't seem to come to me while looking at this. 
-> DONE

=== poi_classroom_has_answer ===
// Checking out Point of Interest Classroom (Has the answer)
Wait a minute... #speaker:Player
-> FoundAnswer ->
This is reminding me of what we learned in class! The answer must be...
-> DONE

=== BookBiology ===
// Interacting with Book (Math)
You found a Biology textbook! 
{currentQuestion == 1: -> book_has_right_answer|-> book_no_right_answer}
-> DONE

=== BookChemistry ===
// Interacting with Book (English)
You found a Chemistry textbook! 
{currentQuestion == 2: -> book_has_right_answer|-> book_no_right_answer}
-> DONE

=== BookScience ===
// Interacting with Book (Science)
You found a Science textbook! 
{currentQuestion == 3: -> book_has_right_answer|-> book_no_right_answer}
-> DONE

=== book_has_right_answer ===
// Interacting with Book (Has the right Answer)
-> FoundAnswer ->
I remember seeing this on the test! So the answer must be... #speaker:Player
-> DONE

=== book_no_right_answer ===
// Interacting with Book (Doesn't have the right answer)
None of these practice questions are what I'm looking for, best to search somewhere else. #speaker:Player
-> DONE

=== resume_time_choice ===
// Played when the player reaches their desk during time stop
Do you want to resume time?

// ADD YES/NO CHOICE HERE
-> DONE

=== failed_exam ===
// Failed the exam (Lose Condition)
I don't really feel good about the answers I submitted, but we'll have to see.. #speaker:Player
~ ExamEndResult(false)
-> DONE

=== passed_exam ===
// Passed the exam (Win Condition)
Okay! Feeling good about these answers. I think I did pretty well! #speaker:Player
~ ExamEndResult(true)
-> DONE