///////////////////////////////////////////////////////////////
const API_BASE_URL = "http://localhost:7178/api";
const Api_Link = "http://localhost:7178/api/subscription";
///////////////////////////////////////////////////////////////


const g = 'f1f9d066-7224-4d43-b68d-5b8f230bc33d'


document.getElementById("formulaire-formation").addEventListener("submit", async function (e) {
    e.preventDefault();

    const form = e.target;
    if (!form.checkValidity()) {
        form.reportValidity();
        return;
    }
});

window.envoyerForm = async function (actionType) {
    const form = document.getElementById('formulaire-formation');
    const formData = new FormData(form);

    // Remplace ceci par ta propre logique pour générer ou récupérer le cke
    const ke = g; // ou une autre méthode valide

    const contact = {
        nom: formData.get("nom"),
        prenom: formData.get("prenom"),
        email: formData.get("adressemail"),
        telephone: formData.get("telephone"),
        entreprise: formData.get("entreprise"),
        poste: formData.get("poste"),
        connaissance: formData.get("connaissance"),
        returnUrl: window.location.href,
        action: actionType,
        cke: ke
    };

    try {
        const response = await fetch(Api_Link, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ contact })
        });

        const result = await response.json();

        if (!response.ok) {
            alert("❌ Erreur : " + (result.error || response.statusText));
            return;
        }

        if (actionType === 'subscription') {
            if (result && result.paymentUrl) {
                window.location.href = result.paymentUrl; // Redirection vers Stripe
            } else {
                alert("❌ Erreur : lien de paiement introuvable.");
            }
        } else {
            alert("✅ Votre demande a été envoyée avec succès !");
            form.reset();
        }


    } catch (error) {
        console.error("Erreur lors de l'envoi du formulaire :", error);
        alert("❌ Une erreur est survenue lors de l'envoi du formulaire.", error);
    }
};
