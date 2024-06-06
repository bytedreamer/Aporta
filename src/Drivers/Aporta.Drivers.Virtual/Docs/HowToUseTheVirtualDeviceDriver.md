# How to Use the Virtual Device Driver

## Virtual Device Configuration
<ol>
  <li>Under the Configuration menu, select Hardware->Drivers.</li>
  <li>Enable the Virtual Device Driver.</li>
</ol>

## Assign the Virtual Device to a door
<ol>
  <li>Under the Configuration menu, select Hardware->Doors.</li>
  <li>Add a new door.</li>
  <li>Assign the door to the Virtual Device reader and output.
</ol>

## Simulate a badge swipe
<ol>
  <li>Under the Configuration menu, select Hardware->Drivers.</li>
  <li>Click on the row for the Virtual Device Driver.</li>
  <li>The configuration page for the Virtual Device Driver will open.</li>
  <li>A list of virtual readers assigned to the Virtual Device Driver will be displayed.
	<ul>
		<li>Each record in this list provides a text field to key in a hypothetical badge number, and a button that 
		when clicked will simulate swiping a badge that holds that number.
		</li>
		<li>
		A record of the badge swipe will be written to a log. You can see a history of badge swipes on the Monitoring page.
		</li>
		<li>
		Records of badge swipes using badge numbers not yet assigned to a person in the system will show "Credential Not Enrolled".
		</li>
		<li>
		To enroll a person in the system with a badge, follow the Quck Start Guide instructions. 
		</li>
	</ul>
  </li>
</ol>

[Quck Start Guide instructions](https://github.com/bytedreamer/Aporta/wiki/Quick-start-guide)
  