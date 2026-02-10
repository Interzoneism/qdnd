START /B /WAIT cmd /c "ng build --output-path docs --base-href ./"
START /B /WAIT cmd /c "copy .\docs\index.html .\docs\404.html"
START /B /WAIT cmd /c "copy .\CNAME .\docs\CNAME"
@pause