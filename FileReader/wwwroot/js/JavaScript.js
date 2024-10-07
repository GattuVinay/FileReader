
var singleImageCheck = document.getElementById('singleImage');
var twoImagesCheck = document.getElementById('twoImages');
var singleFileInput = document.getElementById('singleFileInput');
var twoFileInputs = document.getElementById('twoFileInputs');
var degreeCertificate = document.getElementById('DegreeCertificate');


// Ensure only one checkbox is selected at a time
singleImageCheck.addEventListener('change', function () {
    if (this.checked) {
        twoImagesCheck.checked = false; // Deselect the other checkbox
        toggleFileInputs();
    }
});

twoImagesCheck.addEventListener('change', function () {
    if (this.checked) {
        singleImageCheck.checked = false; 
        toggleFileInputs();
    }
});


function toggleFileInputs() {
    if (singleImageCheck.checked) {
        singleFileInput.style.display = 'block';
        twoFileInputs.style.display = 'none';
    }
    else if (twoImagesCheck.checked) {
        singleFileInput.style.display = 'none';
        twoFileInputs.style.display = 'block';
    }
}


(document).ready(function () {
    toggleFileInputs();

});
