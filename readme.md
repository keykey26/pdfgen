# PDF Generation Service

This is a .net core Azure function app.

#Hosting

This requiers a windows function app as it uses a libury to render the templates that dose not work on Linix.
It also needs a seprate remote Headless Chrome instance to use as the render engine as you can not use it inside the function itself.